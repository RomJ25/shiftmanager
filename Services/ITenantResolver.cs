namespace ShiftManager.Services;

/// <summary>
/// Resolves the current tenant's CompanyId for the request
/// </summary>
public interface ITenantResolver
{
    /// <summary>
    /// Gets the current tenant's CompanyId
    /// </summary>
    int GetCurrentTenantId();

    /// <summary>
    /// Sets the current tenant's CompanyId for this request
    /// </summary>
    void SetCurrentTenantId(int companyId);

    /// <summary>
    /// Checks if a tenant is currently resolved
    /// </summary>
    bool HasTenant();
}