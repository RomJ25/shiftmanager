using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;

namespace ShiftManager.Services;

public interface ICompanyScopeService
{
    int GetCurrentCompanyId(ClaimsPrincipal user);
    Task<AppUser?> GetCompanyUserAsync(int userId, int companyId);
    Task<TimeOffRequest?> GetCompanyTimeOffRequestAsync(int requestId, int companyId);
    Task<SwapRequest?> GetCompanySwapRequestAsync(int requestId, int companyId);
    Task<ShiftAssignment?> GetCompanyShiftAssignmentAsync(int assignmentId, int companyId);
    Task<List<ShiftAssignment>> GetAssignmentsForUserInRangeAsync(int userId, DateOnly startDate, DateOnly endDate, int companyId);
}

public class CompanyScopeService : ICompanyScopeService
{
    private readonly AppDbContext _db;

    public CompanyScopeService(AppDbContext db)
    {
        _db = db;
    }

    public int GetCurrentCompanyId(ClaimsPrincipal user)
    {
        var claim = user.FindFirst("CompanyId")?.Value
            ?? throw new InvalidOperationException("CompanyId claim is missing for the current user.");

        return int.Parse(claim);
    }

    public Task<AppUser?> GetCompanyUserAsync(int userId, int companyId)
    {
        return _db.Users.SingleOrDefaultAsync(u => u.Id == userId && u.CompanyId == companyId);
    }

    public Task<TimeOffRequest?> GetCompanyTimeOffRequestAsync(int requestId, int companyId)
    {
        return (from request in _db.TimeOffRequests
                join user in _db.Users on request.UserId equals user.Id
                where request.Id == requestId && user.CompanyId == companyId
                select request).SingleOrDefaultAsync();
    }

    public Task<SwapRequest?> GetCompanySwapRequestAsync(int requestId, int companyId)
    {
        return (from swap in _db.SwapRequests
                join assignment in _db.ShiftAssignments on swap.FromAssignmentId equals assignment.Id
                join instance in _db.ShiftInstances on assignment.ShiftInstanceId equals instance.Id
                join fromUser in _db.Users on assignment.UserId equals fromUser.Id
                join toUser in _db.Users on swap.ToUserId equals toUser.Id into recipients
                from recipient in recipients.DefaultIfEmpty()
                where swap.Id == requestId
                      && instance.CompanyId == companyId
                      && fromUser.CompanyId == companyId
                      && (recipient == null || recipient.CompanyId == companyId)
                select swap).SingleOrDefaultAsync();
    }

    public Task<ShiftAssignment?> GetCompanyShiftAssignmentAsync(int assignmentId, int companyId)
    {
        return (from assignment in _db.ShiftAssignments
                join instance in _db.ShiftInstances on assignment.ShiftInstanceId equals instance.Id
                join user in _db.Users on assignment.UserId equals user.Id
                where assignment.Id == assignmentId
                      && instance.CompanyId == companyId
                      && user.CompanyId == companyId
                select assignment).SingleOrDefaultAsync();
    }

    public Task<List<ShiftAssignment>> GetAssignmentsForUserInRangeAsync(int userId, DateOnly startDate, DateOnly endDate, int companyId)
    {
        return (from assignment in _db.ShiftAssignments
                join instance in _db.ShiftInstances on assignment.ShiftInstanceId equals instance.Id
                join user in _db.Users on assignment.UserId equals user.Id
                where assignment.UserId == userId
                      && instance.WorkDate >= startDate
                      && instance.WorkDate <= endDate
                      && instance.CompanyId == companyId
                      && user.CompanyId == companyId
                select assignment).ToListAsync();
    }
}
