using ShiftManager.Models.Support;

namespace ShiftManager.Models;

public class RoleAssignmentAudit
{
    public int Id { get; set; }
    public int ChangedBy { get; set; }
    public int TargetUserId { get; set; }
    public UserRole? FromRole { get; set; }
    public UserRole ToRole { get; set; }
    public int CompanyId { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}
