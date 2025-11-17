using ShiftManager.Models;

namespace ShiftManager.Services;

public static class TimeHelpers
{
    public static (DateTime start, DateTime end) GetShiftWindow(ShiftType t, DateOnly date)
    {
        // Use DateOnly.ToDateTime(TimeOnly) – not ToTimeSpan()
        var start = date.ToDateTime(t.Start);
        var end = date.ToDateTime(t.End);
        if (t.End <= t.Start)
        {
            // wraps past midnight
            end = end.AddDays(1);
        }
        return (start, end);
    }

    public static double Hours(ShiftType t)
    {
        var (s, e) = GetShiftWindow(t, DateOnly.FromDateTime(DateTime.Today));
        return (e - s).TotalHours;
    }

    public static DateOnly WeekStart(DateOnly date)
    {
        // Monday as start
        int delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-delta);
    }
}
