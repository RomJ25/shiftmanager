using System.Security.Claims;

namespace ShiftManager.Services;

/// <summary>
/// Provides access to the current company/tenant context for the request.
/// Reads CompanyId from user claims and stores in HttpContext.Items.
/// Multitenancy Phase 2: Context infrastructure
/// </summary>
public class CompanyContext : ICompanyContext
{
    private const string CompanyIdKey = "CompanyId";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CompanyContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? CompanyId
    {
        get
        {
            var httpContext = _httpContextAccessor.HttpContext;
            if (httpContext == null)
                return null;

            // Check if already resolved and stored in HttpContext.Items
            if (httpContext.Items.TryGetValue(CompanyIdKey, out var cachedCompanyId))
            {
                return cachedCompanyId as int?;
            }

            // Resolve from user claims
            var user = httpContext.User;
            if (user?.Identity?.IsAuthenticated == true)
            {
                var companyIdClaim = user.FindFirst("CompanyId");
                if (companyIdClaim != null && int.TryParse(companyIdClaim.Value, out var companyId))
                {
                    // Cache in HttpContext.Items for this request
                    httpContext.Items[CompanyIdKey] = companyId;
                    return companyId;
                }
            }

            return null;
        }
    }

    public int GetCompanyIdOrThrow()
    {
        var companyId = CompanyId;
        if (!companyId.HasValue)
        {
            throw new InvalidOperationException(
                "CompanyId is not available in the current context. " +
                "Ensure the user is authenticated and has a valid CompanyId claim.");
        }

        return companyId.Value;
    }

    public bool HasCompanyContext()
    {
        return CompanyId.HasValue;
    }
}