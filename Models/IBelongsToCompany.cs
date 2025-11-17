namespace ShiftManager.Models;

/// <summary>
/// Marker interface for entities that belong to a specific company/tenant.
/// Entities implementing this interface will have their CompanyId automatically set
/// by the CompanyIdInterceptor during SaveChanges.
/// </summary>
public interface IBelongsToCompany
{
    int CompanyId { get; set; }
}