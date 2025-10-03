using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models.Support;

namespace ShiftManager.Services;

public class DirectorService : IDirectorService
{
    private readonly AppDbContext _db;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public DirectorService(AppDbContext db, IHttpContextAccessor httpContextAccessor)
    {
        _db = db;
        _httpContextAccessor = httpContextAccessor;
    }

    private ClaimsPrincipal? CurrentUser => _httpContextAccessor.HttpContext?.User;

    private int? CurrentUserId
    {
        get
        {
            var userIdClaim = CurrentUser?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return userIdClaim != null ? int.Parse(userIdClaim) : null;
        }
    }

    public bool IsDirector()
    {
        return CurrentUser?.IsInRole(nameof(UserRole.Director)) ?? false;
    }

    public async Task<bool> IsDirectorOfAsync(int companyId)
    {
        if (!IsDirector() || CurrentUserId == null)
            return false;

        return await _db.DirectorCompanies
            .AnyAsync(dc => dc.UserId == CurrentUserId.Value
                         && dc.CompanyId == companyId
                         && !dc.IsDeleted);
    }

    public async Task<List<int>> GetDirectorCompanyIdsAsync()
    {
        if (CurrentUserId == null)
            return new List<int>();

        return await GetDirectorCompanyIdsAsync(CurrentUserId.Value);
    }

    public async Task<List<int>> GetDirectorCompanyIdsAsync(int userId)
    {
        return await _db.DirectorCompanies
            .Where(dc => dc.UserId == userId && !dc.IsDeleted)
            .Select(dc => dc.CompanyId)
            .ToListAsync();
    }

    public async Task<bool> CanManageCompanyAsync(int companyId)
    {
        if (CurrentUserId == null)
            return false;

        // Owner can manage any company
        if (CurrentUser?.IsInRole(nameof(UserRole.Owner)) ?? false)
            return true;

        // Check if user is Director of this company
        if (IsDirector())
        {
            var isDirectorOf = await IsDirectorOfAsync(companyId);
            if (isDirectorOf)
                return true;
        }

        // Check if user is Manager of this company
        if (CurrentUser?.IsInRole(nameof(UserRole.Manager)) ?? false)
        {
            var user = await _db.Users.FindAsync(CurrentUserId.Value);
            return user?.CompanyId == companyId;
        }

        return false;
    }

    public bool CanAssignRole(string role)
    {
        // Directors cannot assign Owner role
        if (IsDirector() && role == nameof(UserRole.Owner))
            return false;

        // Owners can assign any role
        if (CurrentUser?.IsInRole(nameof(UserRole.Owner)) ?? false)
            return true;

        // Directors can assign Manager and Employee
        if (IsDirector() && (role == nameof(UserRole.Manager) || role == nameof(UserRole.Employee)))
            return true;

        // Managers can assign Employee
        if ((CurrentUser?.IsInRole(nameof(UserRole.Manager)) ?? false) && role == nameof(UserRole.Employee))
            return true;

        return false;
    }
}
