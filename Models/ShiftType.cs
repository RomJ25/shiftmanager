using Microsoft.Extensions.Localization;

namespace ShiftManager.Models;

public class ShiftType
{
    public int Id { get; set; }
    public int CompanyId { get; set; }
    public string Key { get; set; } = string.Empty; // MORNING, NOON, NIGHT, MIDDLE
    public string Name { get; set; } = string.Empty; // Display name (can be custom or use Key)
    public string? Description { get; set; } // Optional description
    public TimeOnly Start { get; set; }
    public TimeOnly End { get; set; } // if End <= Start => wraps to next day

    // Navigation property
    public Company? Company { get; set; }

    /// <summary>
    /// Gets the localized display name for this shift type based on the Key
    /// </summary>
    public string GetLocalizedName(IStringLocalizer localizer)
    {
        // Use the localized Key for standard shift types
        return localizer[Key];
    }
}
