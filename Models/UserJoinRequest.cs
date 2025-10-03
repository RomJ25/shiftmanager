using ShiftManager.Models.Support;

namespace ShiftManager.Models;

/// <summary>
/// Represents a pending request from a user to join a company with a specific role.
/// Requires approval from authorized personnel before user account is created.
/// </summary>
public class UserJoinRequest
{
    public int Id { get; set; }

    /// <summary>
    /// Email address of the person requesting access
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Display name for the new user account
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password for the new account
    /// </summary>
    public byte[] PasswordHash { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Password salt
    /// </summary>
    public byte[] PasswordSalt { get; set; } = Array.Empty<byte>();

    /// <summary>
    /// Company the user wants to join
    /// </summary>
    public int CompanyId { get; set; }

    /// <summary>
    /// Role requested by the user
    /// </summary>
    public UserRole RequestedRole { get; set; }

    /// <summary>
    /// Current status of the request
    /// </summary>
    public JoinRequestStatus Status { get; set; } = JoinRequestStatus.Pending;

    /// <summary>
    /// When the request was submitted
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Who approved or rejected the request (null if pending)
    /// </summary>
    public int? ReviewedBy { get; set; }

    /// <summary>
    /// When the request was reviewed (null if pending)
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Reason for rejection (if applicable)
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// The user ID created after approval (null if not yet approved)
    /// </summary>
    public int? CreatedUserId { get; set; }

    // Navigation properties
    public Company? Company { get; set; }
    public AppUser? ReviewedByUser { get; set; }
    public AppUser? CreatedUser { get; set; }
}
