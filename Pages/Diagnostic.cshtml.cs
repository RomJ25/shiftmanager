using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;

namespace ShiftManager.Pages;

public class DiagnosticModel : PageModel
{
    private readonly AppDbContext _db;

    public DiagnosticModel(AppDbContext db) => _db = db;

    public record CompanyInfo(int Id, string Name, string? Slug);
    public record UserData(string Email, int CompanyId, string Role);
    public record ShiftInstanceInfo(int Id, int CompanyId, string WorkDate, string ShiftKey, int Staffing);
    public record AssignmentInfo(int Id, int UserId, int CompanyId, int InstanceId, string WorkDate);

    public List<CompanyInfo> Companies { get; set; } = new();
    public UserData? UserInfo { get; set; }
    public List<ShiftInstanceInfo> ShiftInstances { get; set; } = new();
    public List<AssignmentInfo> Assignments { get; set; } = new();

    public async Task OnGetAsync()
    {
        // Get companies
        Companies = await _db.Companies
            .IgnoreQueryFilters()
            .Select(c => new CompanyInfo(c.Id, c.Name, c.Slug))
            .ToListAsync();

        // Get user b@b
        var user = await _db.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.Email == "b@b");
        if (user != null)
        {
            UserInfo = new UserData(user.Email, user.CompanyId, user.Role.ToString());
        }

        // Get shift instances for October 2025
        ShiftInstances = await (from si in _db.ShiftInstances.IgnoreQueryFilters()
                                join st in _db.ShiftTypes.IgnoreQueryFilters() on si.ShiftTypeId equals st.Id
                                where si.WorkDate >= DateOnly.Parse("2025-10-01") && si.WorkDate <= DateOnly.Parse("2025-10-31")
                                select new ShiftInstanceInfo(
                                    si.Id,
                                    si.CompanyId,
                                    si.WorkDate.ToString(),
                                    st.Key,
                                    si.StaffingRequired
                                )).ToListAsync();

        // Get assignments for b@b
        if (user != null)
        {
            Assignments = await (from sa in _db.ShiftAssignments.IgnoreQueryFilters()
                                 join si in _db.ShiftInstances.IgnoreQueryFilters() on sa.ShiftInstanceId equals si.Id
                                 where sa.UserId == user.Id
                                 select new AssignmentInfo(
                                     sa.Id,
                                     sa.UserId,
                                     sa.CompanyId,
                                     sa.ShiftInstanceId,
                                     si.WorkDate.ToString()
                                 )).ToListAsync();
        }
    }
}
