namespace ShiftManager.Services;

/// <summary>
/// Service for managing "View as Manager" mode for Directors
/// </summary>
public interface IViewAsModeService
{
    /// <summary>
    /// Check if currently in "View as Manager" mode
    /// </summary>
    bool IsViewingAsManager();

    /// <summary>
    /// Get the company ID being viewed as manager (null if not in view mode)
    /// </summary>
    int? GetViewAsCompanyId();

    /// <summary>
    /// Enter "View as Manager" mode for a specific company
    /// Only available to Directors
    /// </summary>
    Task<bool> EnterViewAsModeAsync(int companyId);

    /// <summary>
    /// Exit "View as Manager" mode
    /// </summary>
    Task ExitViewAsModeAsync();

    /// <summary>
    /// Get the company name being viewed as manager
    /// </summary>
    Task<string?> GetViewAsCompanyNameAsync();
}
