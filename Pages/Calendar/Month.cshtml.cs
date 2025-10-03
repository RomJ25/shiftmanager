using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;               // ensure this using is present
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Localization;
using ShiftManager.Resources;

namespace ShiftManager.Pages.Calendar;

[Authorize]
[IgnoreAntiforgeryToken] // ⬅ apply here (class level)

public class MonthModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ICompanyContext _companyContext;
    private readonly ILogger<MonthModel> _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public MonthModel(AppDbContext db, ICompanyContext companyContext, ILogger<MonthModel> logger, IStringLocalizer<SharedResources> localizer)
    {
        _db = db;
        _companyContext = companyContext;
        _logger = logger;
        _localizer = localizer;
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

        var companyId = _companyContext.GetCompanyIdOrThrow();

        // Load shift types
        var types = await _db.ShiftTypes.OrderBy(s => s.Key).ToListAsync();

        // Prepare shift types for JavaScript
        ViewData["ShiftTypes"] = types.Select(t => new
        {
            id = t.Id,
            key = t.Key,
            name = t.Name,
            start = t.Start.ToString("HH:mm"),
            end = t.End.ToString("HH:mm")
        }).ToList();

        // Build 6-week grid starting Monday
        var start = new DateOnly(target.Year, target.Month, 1);
        int delta = ((int)start.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        var gridStart = start.AddDays(-delta);
        var dates = Enumerable.Range(0, 42).Select(i => gridStart.AddDays(i)).ToList();

        // Load instances and assignments for the window
        var instances = await _db.ShiftInstances
            .Where(si => si.CompanyId == companyId && si.WorkDate >= dates.First() && si.WorkDate <= dates.Last())
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

        for (int w = 0; w < 6; w++)
        {
            var week = new List<DayVM>();
            for (int d = 0; d < 7; d++)
            {
                var date = dates[w * 7 + d];
                var vm = new DayVM { Date = date };
                foreach (var t in types)
                {
                    var inst = instances.FirstOrDefault(i => i.WorkDate == date && i.ShiftTypeId == t.Id && i.CompanyId == companyId);
                    var assignedCount = inst != null && dictAssigned.ContainsKey(inst.Id) ? dictAssigned[inst.Id] : 0;
                    var requiredCount = inst?.StaffingRequired ?? 0;
                    var assignedNames = inst != null && dictAssignedNames.ContainsKey(inst.Id) ? dictAssignedNames[inst.Id] : new List<string>();
                    var emptySlots = Enumerable.Repeat(_localizer["Empty"].Value, Math.Max(0, requiredCount - assignedCount)).ToList();

                    vm.Lines.Add(new LineVM
                    {
                        ShiftTypeId = t.Id,
                        InstanceId = inst?.Id ?? 0,
                        Concurrency = inst?.Concurrency ?? 0,
                        ShortName = t.Name[..Math.Min(3, t.Name.Length)],
                        ShiftTypeKey = t.Key.ToLower(),
                        ShiftTypeName = t.Name,
                        ShiftName = inst?.Name ?? "",
                        Assigned = assignedCount,
                        Required = requiredCount,
                        AssignedNames = assignedNames,
                        EmptySlots = emptySlots,
                        StartTime = t.Start.ToString("HH:mm"),
                        EndTime = t.End.ToString("HH:mm")
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
        var companyId = _companyContext.GetCompanyIdOrThrow();
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
