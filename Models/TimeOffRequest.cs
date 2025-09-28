using ShiftManager.Models.Support;

namespace ShiftManager.Models;

public class TimeOffRequest
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public string? Reason { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
