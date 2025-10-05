using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;
using Microsoft.Extensions.Logging;

namespace ShiftManager.Pages.Assignments;

[Authorize(Policy = "IsManagerOrAdmin")]
public class ManageModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConflictChecker _checker;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ManageModel> _logger;
    private readonly ICompanyContext _companyContext;
    private readonly IDirectorService _directorService;
    private readonly ITraineeService _traineeService;

    public ManageModel(AppDbContext db, IConflictChecker checker, INotificationService notificationService, ILogger<ManageModel> logger, ICompanyContext companyContext, IDirectorService directorService, ITraineeService traineeService)
    {
        _db = db;
        _checker = checker;
        _notificationService = notificationService;
        _logger = logger;
        _companyContext = companyContext;
        _directorService = directorService;
        _traineeService = traineeService;
    }


    [BindProperty(SupportsGet = true)] public DateOnly Date { get; set; }
    [BindProperty(SupportsGet = true)] public int ShiftTypeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public ShiftType? Type { get; set; }
    public ShiftInstance Instance { get; set; } = default!;
    public List<(int AssignmentId, string UserLabel, int? TraineeUserId, string? TraineeName)> Assigned { get; set; } = new();

    [BindProperty] public int? SelectedUserId { get; set; }
    [BindProperty] public string ShiftName { get; set; } = string.Empty;
    public List<AppUser> ActiveUsers { get; set; } = new();
    public List<AppUser> Trainees { get; set; } = new();
    public HashSet<int> UsersOnTimeOff { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        // Load the shift type (across all companies using IgnoreQueryFilters)
        Type = await _db.ShiftTypes
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(st => st.Id == ShiftTypeId);

        if (Type == null)
        {
            _logger.LogWarning("ShiftType {ShiftTypeId} not found", ShiftTypeId);
            return RedirectToPage("/Calendar/Month");
        }

        // Company is determined by the shift type's CompanyId
        var companyId = Type.CompanyId;

        // Validate user has access to this company
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var currentUser = await _db.Users.FindAsync(currentUserId);

        bool hasAccess = false;
        if (currentUser!.Role == UserRole.Owner)
        {
            hasAccess = true;
        }
        else if (currentUser.Role == UserRole.Director)
        {
            hasAccess = await _directorService.IsDirectorOfAsync(companyId);
        }
        else if (currentUser.Role == UserRole.Manager)
        {
            hasAccess = currentUser.CompanyId == companyId;
        }

        if (!hasAccess)
        {
            _logger.LogWarning("User {UserId} attempted to access shift for company {CompanyId} without permission", currentUserId, companyId);
            return RedirectToPage("/AccessDenied");
        }

        Instance = await _db.ShiftInstances
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.CompanyId == companyId && i.WorkDate == Date && i.ShiftTypeId == ShiftTypeId)
            ?? new ShiftInstance { CompanyId = companyId, WorkDate = Date, ShiftTypeId = ShiftTypeId, StaffingRequired = 0 };

        if (Instance.Id == 0)
        {
            _db.ShiftInstances.Add(Instance);
            await _db.SaveChangesAsync();
        }

        var assignments = await _db.ShiftAssignments
            .IgnoreQueryFilters()
            .Where(a => a.ShiftInstanceId == Instance.Id)
            .ToListAsync();

        // Load users and trainees separately to avoid query filter issues
        var userIds = assignments.Select(a => a.UserId).ToList();
        var traineeIds = assignments.Where(a => a.TraineeUserId.HasValue).Select(a => a.TraineeUserId!.Value).ToList();
        var allUserIds = userIds.Concat(traineeIds).Distinct().ToList();

        var users = await _db.Users
            .IgnoreQueryFilters()
            .Where(u => allUserIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

        Assigned = assignments.Select(a => (
            a.Id,
            $"{users[a.UserId].DisplayName} ({users[a.UserId].Email})",
            a.TraineeUserId,
            a.TraineeUserId.HasValue && users.ContainsKey(a.TraineeUserId.Value)
                ? users[a.TraineeUserId.Value].DisplayName
                : null
        )).ToList();

        // Load current shift name for display
        ShiftName = Instance.Name ?? string.Empty;

        // Only show users from the shift's company (exclude trainees from regular user list)
        ActiveUsers = await _db.Users
            .Where(u => u.IsActive && u.CompanyId == companyId && u.Role != UserRole.Trainee)
            .OrderBy(u => u.DisplayName)
            .ToListAsync();

        // Load trainees for this company
        Trainees = await _traineeService.GetCompanyTraineesAsync(companyId);

        // Check which users have approved time off on this date (from same company)
        UsersOnTimeOff = (await _db.TimeOffRequests
            .IgnoreQueryFilters()
            .Where(r => r.CompanyId == companyId &&
                       r.Status == RequestStatus.Approved &&
                       r.StartDate <= Date &&
                       r.EndDate >= Date)
            .Select(r => r.UserId)
            .ToListAsync())
            .ToHashSet();

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync(); // reload context for Instance
        _logger.LogInformation("Attempt add user {UserId} to shiftInstance {InstanceId}", SelectedUserId, Instance.Id);

        if (SelectedUserId is null) { Error = "Select a user."; return Page(); }

        // Validate user belongs to the shift's company
        var selectedUser = await _db.Users.FindAsync(SelectedUserId.Value);
        if (selectedUser == null)
        {
            Error = "Selected user not found.";
            return Page();
        }

        if (selectedUser.CompanyId != Instance.CompanyId)
        {
            _logger.LogWarning("Attempted cross-company assignment: User {UserId} (Company {UserCompanyId}) to Shift (Company {ShiftCompanyId})",
                SelectedUserId.Value, selectedUser.CompanyId, Instance.CompanyId);
            Error = "Cannot assign user from a different company to this shift.";
            return Page();
        }

        int assigned = await _db.ShiftAssignments
            .IgnoreQueryFilters()
            .CountAsync(a => a.ShiftInstanceId == Instance.Id);
        if (assigned >= Instance.StaffingRequired)
        {
            Error = "Cannot over-assign. Increase required first.";
            return Page();
        }

        var conflict = await _checker.CanAssignAsync(SelectedUserId.Value, Instance);
        if (!conflict.Allowed)
        {
            _logger.LogWarning("Conflict add user {UserId} to shiftInstance {InstanceId}: {Reasons}",
                       SelectedUserId, Instance.Id, string.Join("; ", conflict.Reasons));
            Error = string.Join(" ", conflict.Reasons);
            return Page();
        }

        _db.ShiftAssignments.Add(new ShiftAssignment
        {
            ShiftInstanceId = Instance.Id,
            UserId = SelectedUserId.Value,
            CompanyId = Instance.CompanyId
        });

        // Set shift name if provided and this is the first assignment
        // Also check for pending shift name from localStorage (from modal creation)
        if (assigned == 0)
        {
            if (!string.IsNullOrWhiteSpace(ShiftName))
            {
                Instance.Name = ShiftName.Trim();
            }
            // Note: Frontend will handle localStorage-based shift names via JavaScript
        }

        await _db.SaveChangesAsync();

        // Send notification to the assigned user
        await _notificationService.CreateShiftAddedNotificationAsync(
            SelectedUserId.Value,
            Type!.Name,
            Date,
            Type.Start,
            Type.End
        );

        return RedirectToPage(new { date = Date, shiftTypeId = ShiftTypeId, returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostRemoveAsync(int assignmentId)
    {
        var a = await _db.ShiftAssignments
            .Include(sa => sa.ShiftInstance)
            .ThenInclude(si => si.ShiftType)
            .FirstOrDefaultAsync(sa => sa.Id == assignmentId);

        if (a != null)
        {
            _logger.LogInformation("Removing assignment {AssignmentId} from shiftInstance {InstanceId}", assignmentId, a.ShiftInstanceId);

            // Send notification before removing
            await _notificationService.CreateShiftRemovedNotificationAsync(
                a.UserId,
                a.ShiftInstance.ShiftType.Name,
                a.ShiftInstance.WorkDate,
                a.ShiftInstance.ShiftType.Start,
                a.ShiftInstance.ShiftType.End
            );

            _db.ShiftAssignments.Remove(a);
            await _db.SaveChangesAsync();
            _logger.LogInformation("Successfully removed assignment {AssignmentId}", assignmentId);
        }
        else
        {
            _logger.LogWarning("Assignment {AssignmentId} not found for removal", assignmentId);
        }

        return !string.IsNullOrEmpty(ReturnUrl) ? Redirect(ReturnUrl) : RedirectToPage(new { date = Date, shiftTypeId = ShiftTypeId });
    }

    public async Task<IActionResult> OnPostAssignTraineeAsync(int assignmentId, int traineeUserId)
    {
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var success = await _traineeService.AssignTraineeToShiftAsync(assignmentId, traineeUserId, currentUserId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to assign trainee. Please check validation rules.";
        }
        else
        {
            TempData["SuccessMessage"] = "Trainee assigned successfully.";
        }

        return RedirectToPage(new { date = Date, shiftTypeId = ShiftTypeId, returnUrl = ReturnUrl });
    }

    public async Task<IActionResult> OnPostRemoveTraineeAsync(int assignmentId)
    {
        var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);

        var success = await _traineeService.RemoveTraineeFromShiftAsync(assignmentId, "Manual", currentUserId);

        if (!success)
        {
            TempData["ErrorMessage"] = "Failed to remove trainee.";
        }
        else
        {
            TempData["SuccessMessage"] = "Trainee removed successfully.";
        }

        return RedirectToPage(new { date = Date, shiftTypeId = ShiftTypeId, returnUrl = ReturnUrl });
    }
}
