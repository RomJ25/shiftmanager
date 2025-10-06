using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;

namespace ShiftManager.Pages;

[Authorize(Policy = "IsAdmin")]
public class DiagnosticModel : PageModel
{
    private readonly AppDbContext _db;

    public DiagnosticModel(AppDbContext db) => _db = db;

    public record CompanyInfo(int Id, string Name, string? Slug);
    public record UserData(int Id, string Email, int CompanyId, string Role);
    public record ShiftInstanceInfo(int Id, int CompanyId, string WorkDate, string ShiftKey, int Staffing);
    public record AssignmentInfo(int Id, int UserId, int CompanyId, int InstanceId, string WorkDate);
    public record UserOption(int Id, string Email, string DisplayName);

    public List<CompanyInfo> Companies { get; set; } = new();
    public List<UserOption> AllUsers { get; set; } = new();
    public UserData? UserInfo { get; set; }
    public List<ShiftInstanceInfo> ShiftInstances { get; set; } = new();
    public List<AssignmentInfo> Assignments { get; set; } = new();

    [BindProperty(SupportsGet = true)]
    public int? SelectedUserId { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? StartDate { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? EndDate { get; set; }

    public async Task OnGetAsync()
    {
        // Get companies
        Companies = await _db.Companies
            .IgnoreQueryFilters()
            .Select(c => new CompanyInfo(c.Id, c.Name, c.Slug))
            .ToListAsync();

        // Get all users for dropdown
        AllUsers = await _db.Users
            .IgnoreQueryFilters()
            .OrderBy(u => u.Email)
            .Select(u => new UserOption(u.Id, u.Email, u.DisplayName))
            .ToListAsync();

        // Set defaults if not provided
        var startDate = DateOnly.TryParse(StartDate, out var parsedStart)
            ? parsedStart
            : DateOnly.FromDateTime(DateTime.Now.AddMonths(-1));

        var endDate = DateOnly.TryParse(EndDate, out var parsedEnd)
            ? parsedEnd
            : DateOnly.FromDateTime(DateTime.Now.AddMonths(1));

        StartDate = startDate.ToString("yyyy-MM-dd");
        EndDate = endDate.ToString("yyyy-MM-dd");

        // Get user info if selected
        if (SelectedUserId.HasValue)
        {
            var user = await _db.Users
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(u => u.Id == SelectedUserId.Value);

            if (user != null)
            {
                UserInfo = new UserData(user.Id, user.Email, user.CompanyId, user.Role.ToString());

                // Get assignments for selected user
                Assignments = await (from sa in _db.ShiftAssignments.IgnoreQueryFilters()
                                     join si in _db.ShiftInstances.IgnoreQueryFilters() on sa.ShiftInstanceId equals si.Id
                                     where sa.UserId == user.Id && si.WorkDate >= startDate && si.WorkDate <= endDate
                                     select new AssignmentInfo(
                                         sa.Id,
                                         sa.UserId,
                                         sa.CompanyId,
                                         sa.ShiftInstanceId,
                                         si.WorkDate.ToString()
                                     )).ToListAsync();
            }
        }

        // Get shift instances for date range
        ShiftInstances = await (from si in _db.ShiftInstances.IgnoreQueryFilters()
                                join st in _db.ShiftTypes.IgnoreQueryFilters() on si.ShiftTypeId equals st.Id
                                where si.WorkDate >= startDate && si.WorkDate <= endDate
                                orderby si.WorkDate
                                select new ShiftInstanceInfo(
                                    si.Id,
                                    si.CompanyId,
                                    si.WorkDate.ToString(),
                                    st.Key,
                                    si.StaffingRequired
                                )).ToListAsync();
    }
}
