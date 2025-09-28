using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models.Support;
using ShiftManager.Services;

namespace ShiftManager.Pages.My;

[Authorize]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    public IndexModel(AppDbContext db) => _db = db;

    public record UpcomingVM(DateOnly Date, string ShiftName, double Hours, bool IsTimeOff = false);
    public List<UpcomingVM> Upcoming { get; set; } = new();

    public double Next4WeeksHours { get; set; }
    public List<(string label, double hours)> ChartWeeks { get; set; } = new();

    public async Task OnGetAsync()
    {
        int userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var upcoming = await (from a in _db.ShiftAssignments
                              join si in _db.ShiftInstances on a.ShiftInstanceId equals si.Id
                              join st in _db.ShiftTypes on si.ShiftTypeId equals st.Id
                              where a.UserId == userId && si.WorkDate >= DateOnly.FromDateTime(DateTime.Today)
                              orderby si.WorkDate
                              select new { si.WorkDate, st.Key, st.Start, st.End }).ToListAsync();

        var upcomingShifts = upcoming.Select(u =>
        {
            var hours = TimeHelpers.Hours(new ShiftManager.Models.ShiftType { Start = u.Start, End = u.End });
            return new UpcomingVM(u.WorkDate, u.Key, hours);
        }).ToList();

        // Get approved time off periods
        var approvedTimeOff = await _db.TimeOffRequests
            .Where(r => r.UserId == userId &&
                       r.Status == RequestStatus.Approved &&
                       r.EndDate >= DateOnly.FromDateTime(DateTime.Today))
            .ToListAsync();

        // Add time off periods to upcoming list
        var timeOffEntries = new List<UpcomingVM>();
        foreach (var timeOff in approvedTimeOff)
        {
            var currentDate = timeOff.StartDate;
            while (currentDate <= timeOff.EndDate)
            {
                timeOffEntries.Add(new UpcomingVM(currentDate, "Time Off", 0, true));
                currentDate = currentDate.AddDays(1);
            }
        }

        // Combine and sort by date
        Upcoming = upcomingShifts.Concat(timeOffEntries)
            .OrderBy(u => u.Date)
            .ToList();

        // Next 4 weeks summary
        var start = DateOnly.FromDateTime(DateTime.Today);
        var weeks = new List<(string label, double hours)>();
        for (int i = 0; i < 4; i++)
        {
            var ws = TimeHelpers.WeekStart(start).AddDays(i * 7);
            var we = ws.AddDays(6);
            var items = Upcoming.Where(u => u.Date >= ws && u.Date <= we);
            var hours = items.Sum(i => i.Hours);
            weeks.Add(($"{ws:MM/dd}", hours));
        }
        ChartWeeks = weeks;
        Next4WeeksHours = weeks.Sum(w => w.hours);
    }
}
