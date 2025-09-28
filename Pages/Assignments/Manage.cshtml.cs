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
    public ManageModel(AppDbContext db, IConflictChecker checker, INotificationService notificationService, ILogger<ManageModel> logger)
    { _db = db; _checker = checker; _notificationService = notificationService; _logger = logger; }


    [BindProperty(SupportsGet = true)] public DateOnly Date { get; set; }
    [BindProperty(SupportsGet = true)] public int ShiftTypeId { get; set; }
    [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

    public ShiftType? Type { get; set; }
    public ShiftInstance Instance { get; set; } = default!;
    public List<(int AssignmentId, string UserLabel)> Assigned { get; set; } = new();

    [BindProperty] public int? SelectedUserId { get; set; }
    [BindProperty] public string ShiftName { get; set; } = string.Empty;
    public List<AppUser> ActiveUsers { get; set; } = new();
    public HashSet<int> UsersOnTimeOff { get; set; } = new();
    public string? Error { get; set; }

    public async Task<IActionResult> OnGetAsync()
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        Type = await _db.ShiftTypes.FindAsync(ShiftTypeId);
        if (Type == null) return RedirectToPage("/Calendar/Month");

        Instance = await _db.ShiftInstances.FirstOrDefaultAsync(i => i.CompanyId == companyId && i.WorkDate == Date && i.ShiftTypeId == ShiftTypeId)
            ?? new ShiftInstance { CompanyId = companyId, WorkDate = Date, ShiftTypeId = ShiftTypeId, StaffingRequired = 0 };

        if (Instance.Id == 0)
        {
            _db.ShiftInstances.Add(Instance);
            await _db.SaveChangesAsync();
        }

        var assignments = await (from a in _db.ShiftAssignments
                                 join u in _db.Users on a.UserId equals u.Id
                                 where a.ShiftInstanceId == Instance.Id
                                 select new { a.Id, u.DisplayName, u.Email }).ToListAsync();
        Assigned = assignments.Select(a => (a.Id, $"{a.DisplayName} ({a.Email})")).ToList();

        // Load current shift name for display
        ShiftName = Instance.Name ?? string.Empty;

        ActiveUsers = await _db.Users.Where(u => u.IsActive && u.CompanyId == companyId).OrderBy(u => u.DisplayName).ToListAsync();

        // Check which users have approved time off on this date
        UsersOnTimeOff = (await _db.TimeOffRequests
            .Where(r => r.Status == RequestStatus.Approved &&
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

        int assigned = await _db.ShiftAssignments.CountAsync(a => a.ShiftInstanceId == Instance.Id);
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

        _db.ShiftAssignments.Add(new ShiftAssignment { ShiftInstanceId = Instance.Id, UserId = SelectedUserId.Value });

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
}
