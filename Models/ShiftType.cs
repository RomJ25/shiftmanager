using System.ComponentModel.DataAnnotations.Schema;

namespace ShiftManager.Models;

public class ShiftType : IBelongsToCompany
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Key { get; set; } = string.Empty; // MORNING, NOON, NIGHT, MIDDLE
    [NotMapped]
    public string Name
    {
        get
        {
            return Key switch
            {
                "MORNING" => "Morning Shift",
                "NOON" => "Afternoon Shift",
                "NIGHT" => "Night Shift",
                "MIDDLE" => "Mid Shift",
                "EVENING" => "Evening Shift",
                _ => Key // fallback to key if no match
            };
        }
        set { } // Empty setter since this is computed
    }
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; } // if End <= Start => wraps to next day
}
