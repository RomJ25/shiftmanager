namespace ShiftManager.Models;

public class Company
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    // Multitenancy Phase 1: URL routing and branding
    public string? Slug { get; set; }
    public string? DisplayName { get; set; }

    // JSON column for company-specific settings overrides
    public string? SettingsJson { get; set; }
}
