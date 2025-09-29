using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Services;

namespace ShiftManager.Pages.Calendar;

[Authorize]
[IgnoreAntiforgeryToken]
public class WeekModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<WeekModel> _logger;
    private readonly ScheduleSummaryService _scheduleSummary;
    public WeekModel(AppDbContext db, ILogger<WeekModel> logger, ScheduleSummaryService scheduleSummary)
    {
        _db = db;
        _logger = logger;
        _scheduleSummary = scheduleSummary;
    }

    public DateOnly CurrentWeekStart { get; set; }
    public (DateOnly WeekStart, string Label) Previous { get; set; }
    public (DateOnly WeekStart, string Label) Next { get; set; }
    public List<DayVM> Days { get; set; } = new();

    public class DayVM
    {
        public DateOnly Date { get; set; }
        public string DayName { get; set; } = "";
        public List<LineVM> Lines { get; set; } = new();
    }

    public class LineVM
    {
        public int ShiftTypeId { get; set; }
        public int InstanceId { get; set; }
        public int Concurrency { get; set; }
        public string ShortName { get; set; } = "";
        public string ShiftTypeKey { get; set; } = "";
        public string ShiftTypeName { get; set; } = "";
        public string ShiftName { get; set; } = ""; // Custom shift instance name
        public int Assigned { get; set; }
        public int Required { get; set; }
        public List<string> AssignedNames { get; set; } = new();
        public List<string> EmptySlots { get; set; } = new();
        public string StartTime { get; set; } = "";
        public string EndTime { get; set; } = "";
    }

    public async Task OnGetAsync(int? year, int? month, int? day)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var targetDate = year.HasValue && month.HasValue && day.HasValue
            ? new DateOnly(year.Value, month.Value, day.Value)
            : today;

        // Get Monday of the week containing the target date
        int daysFromMonday = ((int)targetDate.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        CurrentWeekStart = targetDate.AddDays(-daysFromMonday);

        Previous = (CurrentWeekStart.AddDays(-7), CurrentWeekStart.AddDays(-7).ToString("MMM dd"));
        Next = (CurrentWeekStart.AddDays(7), CurrentWeekStart.AddDays(7).ToString("MMM dd"));

        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        // Generate 7 days for the week
        var weekDates = Enumerable.Range(0, 7).Select(i => CurrentWeekStart.AddDays(i)).ToList();

        var schedule = await _scheduleSummary.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = companyId,
            StartDate = weekDates.First(),
            EndDate = weekDates.Last(),
            IncludeAssignedNames = true,
            IncludeEmptySlots = true
        });

        ViewData["ShiftTypes"] = schedule.ShiftTypes.Select(t => new
        {
            id = t.Id,
            key = t.Key,
            name = t.Name,
            start = t.Start.ToString("HH:mm"),
            end = t.End.ToString("HH:mm")
        }).ToList();

        var dayLookup = schedule.Days.ToDictionary(d => d.Date);

        foreach (var date in weekDates)
        {
            var dayVm = new DayVM
            {
                Date = date,
                DayName = date.DayOfWeek.ToString()
            };

            if (dayLookup.TryGetValue(date, out var summary))
            {
                foreach (var line in summary.Lines)
                {
                    dayVm.Lines.Add(new LineVM
                    {
                        ShiftTypeId = line.ShiftTypeId,
                        InstanceId = line.InstanceId,
                        Concurrency = line.Concurrency,
                        ShortName = line.ShiftTypeShortName,
                        ShiftTypeKey = line.ShiftTypeKey,
                        ShiftTypeName = line.ShiftTypeName,
                        ShiftName = line.ShiftName,
                        Assigned = line.Assigned,
                        Required = line.Required,
                        AssignedNames = line.AssignedNames.ToList(),
                        EmptySlots = line.EmptySlots.ToList(),
                        StartTime = line.StartTime.ToString("HH:mm"),
                        EndTime = line.EndTime.ToString("HH:mm")
                    });
                }
            }
            Days.Add(dayVm);
        }
    }

    public class AdjustPayload
    {
        public string date { get; set; } = "";
        public int shiftTypeId { get; set; }
        public int delta { get; set; }
        public int concurrency { get; set; }
    }


    public async Task<IActionResult> OnPostAdjustAsync([FromBody] AdjustPayload payload)
    {
        _logger.LogInformation("Adjust staffing: date={Date} shiftTypeId={ShiftTypeId} delta={Delta}", payload.date, payload.shiftTypeId, payload.delta);
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        var date = DateOnly.Parse(payload.date);

        var inst = await _db.ShiftInstances.FirstOrDefaultAsync(i => i.CompanyId == companyId && i.WorkDate == date && i.ShiftTypeId == payload.shiftTypeId);
        if (inst == null)
        {
            if (payload.delta < 0)
                return BadRequest(new { message = "Cannot go below zero." });
            inst = new ShiftInstance
            {
                CompanyId = companyId,
                ShiftTypeId = payload.shiftTypeId,
                WorkDate = date,
                StaffingRequired = 0,
                Concurrency = 0,
                UpdatedAt = DateTime.UtcNow
            };
            _db.ShiftInstances.Add(inst);
        }

        // concurrency check
        if (inst.Concurrency != payload.concurrency)
        {
            _logger.LogWarning("Concurrency mismatch for ShiftInstanceId={Id}: sent={Sent}, current={Current}", inst.Id, payload.concurrency, inst.Concurrency);
            return BadRequest(new { message = "Concurrent update detected. Reload the page." });
        }

        int newRequired = inst.StaffingRequired + payload.delta;
        if (newRequired < 0) return BadRequest(new { message = "Cannot go below zero." });

        // prevent dropping below assigned count
        int assigned = await _db.ShiftAssignments.CountAsync(a => a.ShiftInstanceId == inst.Id);
        if (newRequired < assigned)
            return BadRequest(new { message = $"Cannot set required below assigned ({assigned})." });

        inst.StaffingRequired = newRequired;
        inst.Concurrency++;
        inst.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        return new JsonResult(new { required = inst.StaffingRequired, assigned, concurrency = inst.Concurrency });
    }

}
