using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;
using System;
using System.ComponentModel.DataAnnotations;

namespace ShiftManager.Pages.Admin;

[Authorize(Policy = "IsManagerOrAdmin")]
public class UsersModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<UsersModel> _logger;
    private readonly ICompanyScopeService _companyScope;

    public UsersModel(AppDbContext db, ILogger<UsersModel> logger, ICompanyScopeService companyScope)
    {
        _db = db;
        _logger = logger;
        _companyScope = companyScope;
    }

    public record UserVM(int Id, string DisplayName, string Email, string Role, bool IsActive);
    public List<UserVM> Users { get; set; } = new();

    [BindProperty, EmailAddress] public string NewEmail { get; set; } = string.Empty;
    [BindProperty] public string NewDisplayName { get; set; } = string.Empty;
    [BindProperty] public string NewPassword { get; set; } = string.Empty;
    [BindProperty] public string NewRole { get; set; } = UserRole.Employee.ToString();
    public string? Error { get; set; }

    public IReadOnlyList<UserRole> AvailableRoles { get; } = Enum.GetValues<UserRole>();

    public async Task OnGetAsync()
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        Users = await _db.Users
            .Where(u => u.CompanyId == companyId)
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserVM(u.Id, u.DisplayName, u.Email, u.Role.ToString(), u.IsActive))
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAddAsync()
    {
        await OnGetAsync();

        if (string.IsNullOrWhiteSpace(NewEmail) || string.IsNullOrWhiteSpace(NewDisplayName) || string.IsNullOrWhiteSpace(NewPassword))
        {
            Error = "All fields are required.";
            return Page();
        }

        if (await _db.Users.AnyAsync(u => u.Email == NewEmail))
        {
            Error = "Email already exists.";
            return Page();
        }

        var companyId = _companyScope.GetCurrentCompanyId(User);
        var (h, s) = PasswordHasher.CreateHash(NewPassword);

        _db.Users.Add(new AppUser
        {
            CompanyId = companyId,
            Email = NewEmail,
            DisplayName = NewDisplayName,
            Role = Enum.Parse<UserRole>(NewRole),
            IsActive = true,
            PasswordHash = h,
            PasswordSalt = s
        });

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostToggleAsync(int id)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var user = await _companyScope.GetCompanyUserAsync(id, companyId);
        if (user == null) return Forbid();

        user.IsActive = !user.IsActive;
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRoleAsync(int id, string role)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var user = await _companyScope.GetCompanyUserAsync(id, companyId);
        if (user == null) return Forbid();

        user.Role = Enum.Parse<UserRole>(role);
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostResetPasswordAsync(int id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword)) return RedirectToPage();

        var companyId = _companyScope.GetCurrentCompanyId(User);
        var user = await _companyScope.GetCompanyUserAsync(id, companyId);
        if (user == null) return Forbid();

        var (h, s) = PasswordHasher.CreateHash(newPassword);
        user.PasswordHash = h;
        user.PasswordSalt = s;

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteUserAsync(int id)
    {
        using var transaction = await _db.Database.BeginTransactionAsync();

        try
        {
            var currentUserId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
            var companyId = _companyScope.GetCurrentCompanyId(User);

            // Prevent self-deletion
            if (id == currentUserId)
            {
                _logger.LogWarning("User {CurrentUserId} attempted to delete themselves", currentUserId);
                Error = "You cannot delete your own account.";
                await OnGetAsync();
                return Page();
            }

            var user = await _companyScope.GetCompanyUserAsync(id, companyId);
            if (user == null)
            {
                _logger.LogWarning("User {UserId} not found for deletion", id);
                Error = "User not found.";
                await OnGetAsync();
                return Page();
            }

            _logger.LogInformation("Starting deletion of user {UserId} ({UserName}) by admin {CurrentUserId}", id, user.DisplayName, currentUserId);

            // 1. Remove all shift assignments
            var shiftAssignments = await (from sa in _db.ShiftAssignments
                                          join si in _db.ShiftInstances on sa.ShiftInstanceId equals si.Id
                                          where sa.UserId == id && si.CompanyId == companyId
                                          select sa).ToListAsync();
            if (shiftAssignments.Any())
            {
                _logger.LogInformation("Removing {Count} shift assignments for user {UserId}", shiftAssignments.Count, id);
                _db.ShiftAssignments.RemoveRange(shiftAssignments);
            }

            // 2. Delete all time-off requests
            var timeOffRequests = await _db.TimeOffRequests.Where(tor => tor.UserId == id).ToListAsync();
            if (timeOffRequests.Any())
            {
                _logger.LogInformation("Deleting {Count} time-off requests for user {UserId}", timeOffRequests.Count, id);
                _db.TimeOffRequests.RemoveRange(timeOffRequests);
            }

            // 3. Delete all swap requests (both from and to this user)
            var userAssignmentIds = shiftAssignments.Select(sa => sa.Id).ToList();
            var swapRequestsFrom = await _db.SwapRequests.Where(sr => userAssignmentIds.Contains(sr.FromAssignmentId)).ToListAsync();
            var swapRequestsTo = await _db.SwapRequests.Where(sr => sr.ToUserId == id).ToListAsync();

            var allSwapRequests = swapRequestsFrom.Union(swapRequestsTo).Distinct().ToList();
            if (allSwapRequests.Any())
            {
                _logger.LogInformation("Deleting {Count} swap requests related to user {UserId}", allSwapRequests.Count, id);
                _db.SwapRequests.RemoveRange(allSwapRequests);
            }

            // 4. Delete the user
            _logger.LogInformation("Deleting user {UserId} ({UserName})", id, user.DisplayName);
            _db.Users.Remove(user);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.LogInformation("Successfully deleted user {UserId} ({UserName}) and cleaned up all related data", id, user.DisplayName);

            // Use TempData to show success message after redirect
            TempData["SuccessMessage"] = $"User {user.DisplayName} has been successfully deleted along with all their shifts, time-off requests, and swap requests.";

            return RedirectToPage();
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            _logger.LogError(ex, "Error deleting user {UserId}", id);
            Error = "An error occurred while deleting the user. Please try again.";
            await OnGetAsync();
            return Page();
        }
    }
}
