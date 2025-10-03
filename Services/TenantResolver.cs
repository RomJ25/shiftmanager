using System.Security.Claims;

namespace ShiftManager.Services;

/// <summary>
/// Resolves tenant from user's CompanyId claim
/// Phase 2: Single-tenant behavior maintained via user claims
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private int? _tenantIdOverride;

    public TenantResolver(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int GetCurrentTenantId()
    {
        // If explicitly set, use that
        if (_tenantIdOverride.HasValue)
            return _tenantIdOverride.Value;

        // Get from user's CompanyId claim
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var companyIdClaim = user.FindFirst("CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out var companyId))
            {
                return companyId;
            }
        }

        // Fallback to first company (for migration compatibility)
        // TODO Phase 3: Remove this fallback when all requests are authenticated
        return 1;
    }

    public void SetCurrentTenantId(int companyId)
    {
        _tenantIdOverride = companyId;
    }

    public bool HasTenant()
    {
        return _tenantIdOverride.HasValue ||
               _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated == true;
    }
}