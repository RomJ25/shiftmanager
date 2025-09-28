namespace ShiftManager.Models;

public class ShiftAssignment
{
    public int Id { get; set; }
    public int ShiftInstanceId { get; set; }
    public ShiftInstance ShiftInstance { get; set; } = null!;
    public int UserId { get; set; }
    public AppUser User { get; set; } = null!;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
