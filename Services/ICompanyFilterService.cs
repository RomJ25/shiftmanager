namespace ShiftManager.Services;

/// <summary>
/// Service for managing Director's company filter selections
/// </summary>
public interface ICompanyFilterService
{
    /// <summary>
    /// Get the currently selected company IDs for filtering
    /// Returns empty list if no filter is set (meaning show all accessible companies)
    /// </summary>
    Task<List<int>> GetSelectedCompanyIdsAsync();

    /// <summary>
    /// Set the selected company IDs for filtering
    /// </summary>
    Task SetSelectedCompanyIdsAsync(List<int> companyIds);

    /// <summary>
    /// Clear the filter (show all companies)
    /// </summary>
    Task ClearFilterAsync();

    /// <summary>
    /// Get all accessible company IDs for the current user
    /// For Directors: their assigned companies
    /// For Managers: their single company
    /// For Owners: all companies
    /// </summary>
    Task<List<int>> GetAccessibleCompanyIdsAsync();
}
