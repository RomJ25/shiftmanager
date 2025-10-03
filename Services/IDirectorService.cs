namespace ShiftManager.Services;

/// <summary>
/// Service for checking Director permissions
/// </summary>
public interface IDirectorService
{
    /// <summary>
    /// Check if the current user is a Director
    /// </summary>
    bool IsDirector();

    /// <summary>
    /// Check if the current user is a Director of a specific company
    /// </summary>
    Task<bool> IsDirectorOfAsync(int companyId);

    /// <summary>
    /// Get all company IDs that the current user is a Director of
    /// </summary>
    Task<List<int>> GetDirectorCompanyIdsAsync();

    /// <summary>
    /// Get all company IDs that a specific user is a Director of
    /// </summary>
    Task<List<int>> GetDirectorCompanyIdsAsync(int userId);

    /// <summary>
    /// Check if current user can manage a specific company (either as Director, Manager, or Owner of that company)
    /// </summary>
    Task<bool> CanManageCompanyAsync(int companyId);

    /// <summary>
    /// Check if current user can assign the specified role
    /// Directors cannot assign Owner role
    /// </summary>
    bool CanAssignRole(string role);
}
