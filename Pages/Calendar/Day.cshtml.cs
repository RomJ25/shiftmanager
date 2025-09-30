using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using Microsoft.Extensions.Logging;

namespace ShiftManager.Pages.Calendar;

[Authorize]
[IgnoreAntiforgeryToken]
public class DayModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<DayModel> _logger;
    public DayModel(AppDbContext db, ILogger<DayModel> logger) { _db = db; _logger = logger; }

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

        // Load shift types with custom ordering: morning, middle, noon, night
        var types = await _db.ShiftTypes
            .Where(st => st.CompanyId == companyId)
            .ToListAsync();
        types = types.OrderBy(s => s.Key switch
        {
            "MORNING" => 1,
            "MIDDLE" => 2,
            "NOON" => 3,
            "NIGHT" => 4,
            _ => 99
        }).ToList();

        // Prepare shift types for JavaScript
        ViewData["ShiftTypes"] = types.Select(t => new
        {
            id = t.Id,
            key = t.Key,
            name = t.Name,
            start = t.Start.ToString("HH:mm"),
            end = t.End.ToString("HH:mm")
        }).ToList();

        // Load instances and assignments for the day
        var instances = await _db.ShiftInstances
            .Where(si => si.CompanyId == companyId && si.WorkDate == CurrentDate)
            .ToListAsync();

        var instanceIds = instances.Select(i => i.Id).ToList();
        var assignmentCounts = await _db.ShiftAssignments
            .Where(a => instanceIds.Contains(a.ShiftInstanceId))
            .GroupBy(a => a.ShiftInstanceId)
            .Select(g => new { ShiftInstanceId = g.Key, Count = g.Count() })
            .ToListAsync();

        // Fetch assignments with user names
        var assignmentsWithNames = await (from a in _db.ShiftAssignments
                                         join u in _db.Users on a.UserId equals u.Id
                                         where instanceIds.Contains(a.ShiftInstanceId)
                                         select new { a.ShiftInstanceId, UserName = u.DisplayName })
                                         .ToListAsync();

        var dictAssigned = assignmentCounts.ToDictionary(x => x.ShiftInstanceId, x => x.Count);
        var dictAssignedNames = assignmentsWithNames
            .GroupBy(x => x.ShiftInstanceId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.UserName).ToList());

        foreach (var t in types)
        {
            var inst = instances.FirstOrDefault(i => i.ShiftTypeId == t.Id);
            var assignedCount = inst != null && dictAssigned.ContainsKey(inst.Id) ? dictAssigned[inst.Id] : 0;
            var requiredCount = inst?.StaffingRequired ?? 0;
            var assignedNames = inst != null && dictAssignedNames.ContainsKey(inst.Id) ? dictAssignedNames[inst.Id] : new List<string>();
            var emptySlots = Enumerable.Repeat("Empty", Math.Max(0, requiredCount - assignedCount)).ToList();

            Lines.Add(new LineVM
            {
                ShiftTypeId = t.Id,
                InstanceId = inst?.Id ?? 0,
                Concurrency = inst?.Concurrency ?? 0,
                Name = t.Name,
                ShortName = t.Name[..Math.Min(3, t.Name.Length)],
                ShiftTypeKey = t.Key.ToLower(),
                ShiftTypeName = t.Name,
                ShiftName = inst?.Name ?? "",
                StartTime = t.Start,
                EndTime = t.End,
                StartTimeString = t.Start.ToString("HH:mm"),
                EndTimeString = t.End.ToString("HH:mm"),
                Assigned = assignedCount,
                Required = requiredCount,
                AssignedNames = assignedNames,
                EmptySlots = emptySlots
            });
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