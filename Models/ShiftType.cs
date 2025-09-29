using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftManager.Models;

public class ShiftType
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public Company Company { get; set; } = null!;
    public string Key { get; set; } = string.Empty; // MORNING, NOON, NIGHT, MIDDLE
    public string Name { get; set; } = string.Empty;

    [NotMapped]
    public string DefaultName => Key switch
    {
        "MORNING" => "Morning Shift",
        "NOON" => "Afternoon Shift",
        "NIGHT" => "Night Shift",
        "MIDDLE" => "Mid Shift",
        "EVENING" => "Evening Shift",
        _ => Key // fallback to key if no match
    };

    [NotMapped]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? DefaultName : Name;

    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; } // if End <= Start => wraps to next day
}
