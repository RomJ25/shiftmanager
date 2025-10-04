using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace ShiftManager.Services;

/// <summary>
/// Resolves tenant from user's CompanyId claim
/// Phase 2: Single-tenant behavior maintained via user claims
/// </summary>
public class TenantResolver : ITenantResolver
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly ILogger<TenantResolver>? _logger;
    private int? _tenantIdOverride;

    public TenantResolver(IHttpContextAccessor httpContextAccessor, ILogger<TenantResolver>? logger = null)
    {
        _httpContextAccessor = httpContextAccessor;
        _logger = logger;
    }

    public int GetCurrentTenantId()
    {
        // If explicitly set, use that
        if (_tenantIdOverride.HasValue)
        {
            _logger?.LogInformation("TenantResolver: Using override CompanyId={CompanyId}", _tenantIdOverride.Value);
            return _tenantIdOverride.Value;
        }

        // Get from user's CompanyId claim
        var user = _httpContextAccessor.HttpContext?.User;
        if (user?.Identity?.IsAuthenticated == true)
        {
            var email = user.FindFirst(ClaimTypes.Name)?.Value;
            var companyIdClaim = user.FindFirst("CompanyId");
            if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out var companyId))
            {
                _logger?.LogInformation("TenantResolver: User {Email} has CompanyId claim={CompanyId}", email, companyId);
                return companyId;
            }
            else
            {
                _logger?.LogWarning("TenantResolver: User {Email} authenticated but CompanyId claim missing or invalid! Claim value: {ClaimValue}",
                    email, companyIdClaim?.Value ?? "null");
            }
        }
        else
        {
            _logger?.LogInformation("TenantResolver: User not authenticated, using fallback CompanyId=1");
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