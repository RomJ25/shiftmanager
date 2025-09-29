using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;

namespace ShiftManager.Services;

public class ScheduleSummaryRequest
{
    public int CompanyId { get; init; }
    public DateOnly StartDate { get; init; }
    public DateOnly EndDate { get; init; }
    public bool IncludeAssignedNames { get; init; } = true;
    public bool IncludeEmptySlots { get; init; } = true;
    public IReadOnlyCollection<int>? ShiftTypeIds { get; init; }
}

public class ScheduleSummaryResult
{
    public IReadOnlyList<ShiftTypeSummaryDto> ShiftTypes { get; init; } = Array.Empty<ShiftTypeSummaryDto>();
    public IReadOnlyList<ShiftSummaryDayDto> Days { get; init; } = Array.Empty<ShiftSummaryDayDto>();
}

public class ShiftTypeSummaryDto
{
    public int Id { get; init; }
    public string Key { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public TimeOnly Start { get; init; }
    public TimeOnly End { get; init; }
    public string ShortName { get; init; } = string.Empty;
}

public class ShiftSummaryDayDto
{
    public DateOnly Date { get; init; }
    public IReadOnlyList<ShiftSummaryLineDto> Lines { get; init; } = Array.Empty<ShiftSummaryLineDto>();
}

public class ShiftSummaryLineDto
{
    public int ShiftTypeId { get; init; }
    public string ShiftTypeKey { get; init; } = string.Empty;
    public string ShiftTypeName { get; init; } = string.Empty;
    public string ShiftTypeShortName { get; init; } = string.Empty;
    public int InstanceId { get; init; }
    public int Concurrency { get; init; }
    public string ShiftName { get; init; } = string.Empty;
    public TimeOnly StartTime { get; init; }
    public TimeOnly EndTime { get; init; }
    public int Assigned { get; init; }
    public int Required { get; init; }
    public IReadOnlyList<string> AssignedNames { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> EmptySlots { get; init; } = Array.Empty<string>();
}

public class ScheduleSummaryService
{
    private readonly AppDbContext _db;

    public ScheduleSummaryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ScheduleSummaryResult> QueryAsync(ScheduleSummaryRequest request, CancellationToken cancellationToken = default)
    {
        if (request.EndDate < request.StartDate)
        {
            throw new ArgumentException("EndDate must be on or after StartDate.", nameof(request));
        }

        var shiftTypeFilter = request.ShiftTypeIds?.ToHashSet();

        var shiftTypeQuery = _db.ShiftTypes
            .AsNoTracking()
            .Where(t => t.CompanyId == request.CompanyId);
        if (shiftTypeFilter is { Count: > 0 })
        {
            shiftTypeQuery = shiftTypeQuery.Where(t => shiftTypeFilter.Contains(t.Id));
        }

        var shiftTypes = await shiftTypeQuery
            .OrderBy(t => t.Key)
            .ToListAsync(cancellationToken);

        var dateCount = request.EndDate.DayNumber - request.StartDate.DayNumber + 1;
        var dayRange = Enumerable.Range(0, dateCount)
            .Select(offset => request.StartDate.AddDays(offset))
            .ToList();

        var instanceQuery = _db.ShiftInstances.AsNoTracking()
            .Where(si => si.CompanyId == request.CompanyId
                         && si.WorkDate >= request.StartDate
                         && si.WorkDate <= request.EndDate);
        if (shiftTypeFilter is { Count: > 0 })
        {
            instanceQuery = instanceQuery.Where(si => shiftTypeFilter.Contains(si.ShiftTypeId));
        }

        var instances = await instanceQuery.ToListAsync(cancellationToken);
        var instancesByKey = instances.ToDictionary(i => (i.WorkDate, i.ShiftTypeId));
        var instanceIds = instances.Select(i => i.Id).ToList();

        var assignmentCounts = new Dictionary<int, int>();
        Dictionary<int, List<string>> assignmentNames = new();

        if (instanceIds.Count > 0)
        {
            var counts = await _db.ShiftAssignments.AsNoTracking()
                .Where(sa => instanceIds.Contains(sa.ShiftInstanceId))
                .GroupBy(sa => sa.ShiftInstanceId)
                .Select(g => new { ShiftInstanceId = g.Key, Count = g.Count() })
                .ToListAsync(cancellationToken);

            assignmentCounts = counts.ToDictionary(x => x.ShiftInstanceId, x => x.Count);

            if (request.IncludeAssignedNames)
            {
                var names = await _db.ShiftAssignments.AsNoTracking()
                    .Where(sa => instanceIds.Contains(sa.ShiftInstanceId))
                    .Select(sa => new { sa.ShiftInstanceId, sa.User.DisplayName })
                    .ToListAsync(cancellationToken);

                assignmentNames = names
                    .GroupBy(x => x.ShiftInstanceId)
                    .ToDictionary(g => g.Key, g => g.Select(x => x.DisplayName).ToList());
            }
        }

        var days = new List<ShiftSummaryDayDto>(dayRange.Count);
        foreach (var date in dayRange)
        {
            var lines = new List<ShiftSummaryLineDto>(shiftTypes.Count);
            foreach (var type in shiftTypes)
            {
                instancesByKey.TryGetValue((date, type.Id), out var inst);
                var hasInstance = inst != null;

                var assigned = hasInstance && assignmentCounts.TryGetValue(inst!.Id, out var count) ? count : 0;
                var required = inst?.StaffingRequired ?? 0;
                var emptyCount = Math.Max(0, required - assigned);

                IReadOnlyList<string> names = Array.Empty<string>();
                if (request.IncludeAssignedNames && hasInstance && assignmentNames.TryGetValue(inst!.Id, out var list))
                {
                    names = list;
                }

                IReadOnlyList<string> emptySlots = request.IncludeEmptySlots
                    ? Enumerable.Repeat("Empty", emptyCount).ToList()
                    : Array.Empty<string>();

                lines.Add(new ShiftSummaryLineDto
                {
                    ShiftTypeId = type.Id,
                    ShiftTypeKey = type.Key.ToLowerInvariant(),
                    ShiftTypeName = type.Name,
                    ShiftTypeShortName = Shorten(type.Name),
                    InstanceId = inst?.Id ?? 0,
                    Concurrency = inst?.Concurrency ?? 0,
                    ShiftName = inst?.Name ?? string.Empty,
                    StartTime = type.Start,
                    EndTime = type.End,
                    Assigned = assigned,
                    Required = required,
                    AssignedNames = names,
                    EmptySlots = emptySlots
                });
            }

            days.Add(new ShiftSummaryDayDto
            {
                Date = date,
                Lines = lines
            });
        }

        var shiftTypeDtos = shiftTypes
            .Select(t => new ShiftTypeSummaryDto
            {
                Id = t.Id,
                Key = t.Key,
                Name = t.Name,
                Start = t.Start,
                End = t.End,
                ShortName = Shorten(t.Name)
            })
            .ToList();

        return new ScheduleSummaryResult
        {
            ShiftTypes = shiftTypeDtos,
            Days = days
        };
    }

    private static string Shorten(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        return name.Length <= 3 ? name : name[..3];
    }
}
