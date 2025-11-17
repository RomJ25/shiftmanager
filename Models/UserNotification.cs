using System.ComponentModel.DataAnnotations;
using ShiftManager.Models.Support;

namespace ShiftManager.Models;

public class UserNotification : IBelongsToCompany
{
    public int Id { get; set; }

    // Multitenancy Phase 1: Tenant scoping
    public int CompanyId { get; set; }

    [Required]
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;

    [Required]
    public NotificationType Type { get; set; }

    [Required]
    [StringLength(500)]
    public string Title { get; set; } = "";

    [Required]
    [StringLength(1000)]
    public string Message { get; set; } = "";

    public bool IsRead { get; set; } = false;

    [Required]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ReadAt { get; set; }

    // Optional reference fields for linking back to specific entities
    public int? RelatedEntityId { get; set; }

    [StringLength(50)]
    public string? RelatedEntityType { get; set; } // "TimeOffRequest", "SwapRequest", "ShiftAssignment"
}