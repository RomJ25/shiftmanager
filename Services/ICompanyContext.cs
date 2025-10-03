namespace ShiftManager.Services;

/// <summary>
/// Provides access to the current company/tenant context for the request.
/// Multitenancy Phase 2: Context infrastructure
/// </summary>
public interface ICompanyContext
{
    /// <summary>
    /// Gets the current company ID, or null if not available
    /// </summary>
    int? CompanyId { get; }

    /// <summary>
    /// Gets the current company ID, throwing an exception if not available
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when CompanyId is not available</exception>
    int GetCompanyIdOrThrow();

    /// <summary>
    /// Checks if a company context is available
    /// </summary>
    bool HasCompanyContext();
}