using ShiftManager.Models.Support;

namespace ShiftManager.Models;

public class SwapRequest
{
    public int Id { get; set; }
    public int FromAssignmentId { get; set; } // The original assignment owned by the requesting employee
    public int? ToUserId { get; set; }        // The user they propose to take their shift (null = open offer)
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
