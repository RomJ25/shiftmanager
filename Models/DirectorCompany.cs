namespace ShiftManager.Models;

/// <summary>
/// Maps Directors to Companies they oversee.
/// A Director can manage multiple companies.
/// </summary>
public class DirectorCompany
{
    public int Id { get; set; }

    /// <summary>
    /// The Director user
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// The Company the Director oversees
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Who granted this Director access (typically an Owner)
    /// </summary>
    public int GrantedBy { get; set; }

    /// <summary>
    /// When access was granted
    /// </summary>
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Soft delete flag
    /// </summary>
    public bool IsDeleted { get; set; } = false;

    /// <summary>
    /// When this assignment was soft-deleted (null if active)
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    // Navigation properties
    public AppUser? User { get; set; }
    public Company? Company { get; set; }
    public AppUser? GrantedByUser { get; set; }
}
