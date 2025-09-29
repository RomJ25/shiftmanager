using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Pages.Calendar;

[Authorize]
[IgnoreAntiforgeryToken]
public class DayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<DayModel> _logger;
    private readonly ScheduleSummaryService _scheduleSummary;
    public DayModel(AppDbContext db, ILogger<DayModel> logger, ScheduleSummaryService scheduleSummary)
    {
        _db = db;
        _logger = logger;
        _scheduleSummary = scheduleSummary;
    }

    public DateOnly CurrentDate { get; set; }
    public (DateOnly Date, string Label) Previous { get; set; }
    public (DateOnly Date, string Label) Next { get; set; }
    public List<LineVM> Lines { get; set; } = new();

    public class LineVM
    {
        public int ShiftTypeId { get; set; }
        public int InstanceId { get; set; }
        public int Concurrency { get; set; }
        public string Name { get; set; } = "";
        public string ShortName { get; set; } = "";
        public string ShiftTypeKey { get; set; } = "";
        public string ShiftTypeName { get; set; } = "";
        public string ShiftName { get; set; } = ""; // Custom shift instance name
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public string StartTimeString { get; set; } = "";
        public string EndTimeString { get; set; } = "";
        public int Assigned { get; set; }
        public int Required { get; set; }
        public List<string> AssignedNames { get; set; } = new();
        public List<string> EmptySlots { get; set; } = new();
    }

    public async Task OnGetAsync(int? year, int? month, int? day)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        CurrentDate = year.HasValue && month.HasValue && day.HasValue
            ? new DateOnly(year.Value, month.Value, day.Value)
            : today;

        Previous = (CurrentDate.AddDays(-1), CurrentDate.AddDays(-1).ToString("MMM dd"));
        Next = (CurrentDate.AddDays(1), CurrentDate.AddDays(1).ToString("MMM dd"));

        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        // Query schedule summary for this day
        var schedule = await _scheduleSummary.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = companyId,
            StartDate = CurrentDate,
            EndDate = CurrentDate,
            IncludeAssignedNames = true,
            IncludeEmptySlots = true
        });

        // Provide shift types for frontend
        ViewData["ShiftTypes"] = schedule.ShiftTypes
            .OrderBy(t =>
            {
                // Custom order: morning, middle, noon, night
                return t.Key.ToUpper() switch
                {
                    "MORNING" => 1,
                    "MIDDLE" => 2,
                    "NOON" => 3,
                    "NIGHT" => 4,
                    _ => 99
                };
            })
            .ThenBy(t => t.Name)
            .Select(t => new
            {
                id = t.Id,
                key = t.Key,
                name = t.Name,
                start = t.Start.ToString("HH:mm"),
                end = t.End.ToString("HH:mm")
            })
            .ToList();

        var daySummary = schedule.Days.FirstOrDefault();
        if (daySummary != null)
        {
            var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
            {
                ["MORNING"] = 1,
                ["MIDDLE"] = 2,
                ["NOON"] = 3,
                ["NIGHT"] = 4
            };

            foreach (var line in daySummary.Lines
                         .OrderBy(l => order.TryGetValue(l.ShiftTypeKey, out var rank) ? rank : 99)
                         .ThenBy(l => l.ShiftTypeName))
            {
                Lines.Add(new LineVM
                {
                    ShiftTypeId = line.ShiftTypeId,
                    InstanceId = line.InstanceId,
                    Concurrency = line.Concurrency,
                    Name = line.ShiftTypeName,
                    ShortName = line.ShiftTypeShortName,
                    ShiftTypeKey = line.ShiftTypeKey,
                    ShiftTypeName = line.ShiftTypeName,
                    ShiftName = line.ShiftName,
                    StartTime = line.StartTime,
                    EndTime = line.EndTime,
                    StartTimeString = line.StartTime.ToString("HH:mm"),
                    EndTimeString = line.EndTime.ToString("HH:mm"),
                    Assigned = line.Assigned,
                    Required = line.Required,
                    AssignedNames = line.AssignedNames.ToList(),
                    EmptySlots = line.EmptySlots.ToList()
                });
            }
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
