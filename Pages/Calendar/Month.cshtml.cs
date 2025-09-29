using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;               // ensure this using is present
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Services;

namespace ShiftManager.Pages.Calendar;

[Authorize]
[IgnoreAntiforgeryToken] // ⬅ apply here (class level)

public class MonthModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<MonthModel> _logger;
    private readonly ScheduleSummaryService _scheduleSummary;
    public MonthModel(AppDbContext db, ILogger<MonthModel> logger, ScheduleSummaryService scheduleSummary)
    {
        _db = db;
        _logger = logger;
        _scheduleSummary = scheduleSummary;
    }


    public DateOnly CurrentMonth { get; set; }
    public (DateOnly MonthYear, string Label) Previous { get; set; }
    public (DateOnly MonthYear, string Label) Next { get; set; }

    public List<List<DayVM>> Weeks { get; set; } = new();

    public class DayVM
    {
        public DateOnly Date { get; set; }
        public List<LineVM> Lines { get; set; } = new();
    }
    public class LineVM
    {
        public int ShiftTypeId { get; set; }
        public int InstanceId { get; set; } // 0 if none yet
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

    public async Task OnGetAsync(int? year, int? month)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var target = year.HasValue && month.HasValue ? new DateOnly(year.Value, month.Value, 1) : new DateOnly(today.Year, today.Month, 1);
        CurrentMonth = target;

        Previous = (target.AddMonths(-1), target.AddMonths(-1).ToString("MMM yyyy"));
        Next = (target.AddMonths(1), target.AddMonths(1).ToString("MMM yyyy"));

        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        // Build 6-week grid starting Monday
        var start = new DateOnly(target.Year, target.Month, 1);
        int delta = ((int)start.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var gridStart = start.AddDays(-delta);
        var dates = Enumerable.Range(0, 42).Select(i => gridStart.AddDays(i)).ToList();

        var schedule = await _scheduleSummary.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = companyId,
            StartDate = dates.First(),
            EndDate = dates.Last(),
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

        for (int w = 0; w < 6; w++)
        {
            var week = new List<DayVM>();
            for (int d = 0; d < 7; d++)
            {
                var date = dates[w * 7 + d];
                var vm = new DayVM { Date = date };
                if (!dayLookup.TryGetValue(date, out var summary))
                {
                    summary = new ShiftSummaryDayDto { Date = date };
                }

                foreach (var line in summary.Lines)
                {
                    vm.Lines.Add(new LineVM
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
                week.Add(vm);
            }
            Weeks.Add(week);
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
