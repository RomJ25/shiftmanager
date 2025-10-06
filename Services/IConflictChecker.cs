using ShiftManager.Models;

namespace ShiftManager.Services;

public record ConflictResult(bool Allowed, List<string> Reasons)
{
    public static ConflictResult Fail(params string[] reasons) => new(false, reasons.ToList());
    public static ConflictResult Ok() => new(true, new());
}

public interface IConflictChecker
{
    Task<ConflictResult> CanAssignAsync(int userId, ShiftInstance instance, CancellationToken ct = default);
}
