using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;

namespace ShiftManager.Services;

public class ViewAsModeService : IViewAsModeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDirectorService _directorService;
    private readonly AppDbContext _db;
    private const string ViewAsCookieName = "director_view_as_mode";

    public ViewAsModeService(
        IHttpContextAccessor httpContextAccessor,
        IDirectorService directorService,
        AppDbContext db)
    {
        _httpContextAccessor = httpContextAccessor;
        _directorService = directorService;
        _db = db;
    }

    public bool IsViewingAsManager()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        return httpContext?.Request.Cookies.ContainsKey(ViewAsCookieName) ?? false;
    }

    public int? GetViewAsCompanyId()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext == null)
            return null;

        if (httpContext.Request.Cookies.TryGetValue(ViewAsCookieName, out var value)
            && int.TryParse(value, out var companyId))
        {
            return companyId;
        }

        return null;
    }

    public async Task<bool> EnterViewAsModeAsync(int companyId)
    {
        // Only Directors can use this mode
        if (!_directorService.IsDirector())
            return false;

        // Verify Director has access to this company
        var hasAccess = await _directorService.IsDirectorOfAsync(companyId);
        if (!hasAccess)
            return false;

        // Verify company exists
        var companyExists = await _db.Companies.AnyAsync(c => c.Id == companyId);
        if (!companyExists)
            return false;

        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Response.Cookies.Append(ViewAsCookieName, companyId.ToString(), new CookieOptions
            {
                MaxAge = TimeSpan.FromHours(8), // Auto-expire after 8 hours
                HttpOnly = true,
                SameSite = SameSiteMode.Strict
            });
        }

        return true;
    }

    public Task ExitViewAsModeAsync()
    {
        var httpContext = _httpContextAccessor.HttpContext;
        if (httpContext != null)
        {
            httpContext.Response.Cookies.Delete(ViewAsCookieName);
        }
        return Task.CompletedTask;
    }

    public async Task<string?> GetViewAsCompanyNameAsync()
    {
        var companyId = GetViewAsCompanyId();
        if (companyId == null)
            return null;

        var company = await _db.Companies.FindAsync(companyId.Value);
        return company?.Name;
    }
}
