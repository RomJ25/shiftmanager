using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftManager.Models;

public class ShiftInstance
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public int ShiftTypeId { get; set; }
    public ShiftType ShiftType { get; set; } = null!;
    public DateOnly WorkDate { get; set; }
    public string Name { get; set; } = string.Empty; // Custom name for this specific shift instance

    public int StaffingRequired { get; set; } = 0;

    [ConcurrencyCheck]
    public int Concurrency { get; set; } = 0;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
