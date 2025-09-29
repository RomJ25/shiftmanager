using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;

namespace ShiftManager.Pages.Requests;

[Authorize(Policy = "IsManagerOrAdmin")]
public class IndexModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IConflictChecker _checker;
    private readonly INotificationService _notificationService;
    private readonly ILogger<IndexModel> _logger;
    private readonly ICompanyScopeService _companyScope;

    public IndexModel(
        AppDbContext db,
        IConflictChecker checker,
        INotificationService notificationService,
        ILogger<IndexModel> logger,
        ICompanyScopeService companyScope)
    {
        _db = db;
        _checker = checker;
        _notificationService = notificationService;
        _logger = logger;
        _companyScope = companyScope;
    }

    public record TimeOffVM(int Id, string UserName, DateOnly StartDate, DateOnly EndDate, string? Reason);

    public record SwapVM(
        int Id,
        string FromUser,
        DateOnly ShiftDate,
        string ShiftName,
        TimeOnly Start,
        TimeOnly End,
        string? RecipientName,
        string? RecipientEmail);

    public List<TimeOffVM> TimeOff { get; private set; } = new();
    public List<SwapVM> Swaps { get; private set; } = new();
    public string? Error { get; set; }

    public async Task OnGetAsync()
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);

        var timeOffRows = await (from request in _db.TimeOffRequests
                                 join user in _db.Users on request.UserId equals user.Id
                                 where user.CompanyId == companyId
                                       && request.Status == RequestStatus.Pending
                                 orderby request.CreatedAt
                                 select new { Request = request, User = user })
                                .ToListAsync();

        TimeOff = timeOffRows
            .Select(r => new TimeOffVM(
                r.Request.Id,
                FormatUserName(r.User),
                r.Request.StartDate,
                r.Request.EndDate,
                r.Request.Reason))
            .ToList();

        var swapRows = await (from swap in _db.SwapRequests
                              join assignment in _db.ShiftAssignments on swap.FromAssignmentId equals assignment.Id
                              join fromUser in _db.Users on assignment.UserId equals fromUser.Id
                              join instance in _db.ShiftInstances on assignment.ShiftInstanceId equals instance.Id
                              join shiftType in _db.ShiftTypes on instance.ShiftTypeId equals shiftType.Id
                              join toUser in _db.Users on swap.ToUserId equals toUser.Id into recipientJoin
                              from recipient in recipientJoin.DefaultIfEmpty()
                              where instance.CompanyId == companyId
                                    && fromUser.CompanyId == companyId
                                    && (recipient == null || recipient.CompanyId == companyId)
                                    && swap.Status == RequestStatus.Pending
                              orderby instance.WorkDate, shiftType.Start
                              select new
                              {
                                  Swap = swap,
                                  FromUser = fromUser,
                                  Instance = instance,
                                  ShiftType = shiftType,
                                  Recipient = recipient
                              })
                             .ToListAsync();

        Swaps = swapRows
            .Select(r => new SwapVM(
                r.Swap.Id,
                FormatUserName(r.FromUser),
                r.Instance.WorkDate,
                r.ShiftType.DisplayName,
                r.ShiftType.Start,
                r.ShiftType.End,
                r.Recipient == null ? null : FormatUserName(r.Recipient),
                r.Recipient?.Email))
            .ToList();
    }

    public async Task<IActionResult> OnPostApproveTimeOffAsync(int id)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var request = await _companyScope.GetCompanyTimeOffRequestAsync(id, companyId);
        if (request == null) return Forbid();

        request.Status = RequestStatus.Approved;
        await _db.SaveChangesAsync();

        await _notificationService.CreateTimeOffNotificationAsync(
            request.UserId,
            RequestStatus.Approved,
            request.StartDate,
            request.EndDate,
            request.Id);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeclineTimeOffAsync(int id)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var request = await _companyScope.GetCompanyTimeOffRequestAsync(id, companyId);
        if (request == null) return Forbid();

        request.Status = RequestStatus.Declined;
        await _db.SaveChangesAsync();

        await _notificationService.CreateTimeOffNotificationAsync(
            request.UserId,
            RequestStatus.Declined,
            request.StartDate,
            request.EndDate,
            request.Id);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostApproveSwapAsync(int id)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var swap = await _companyScope.GetCompanySwapRequestAsync(id, companyId);
        if (swap == null) return Forbid();

        var assignment = await _companyScope.GetCompanyShiftAssignmentAsync(swap.FromAssignmentId, companyId);
        if (assignment == null)
        {
            swap.Status = RequestStatus.Declined;
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        var instance = await _db.ShiftInstances
            .SingleOrDefaultAsync(i => i.Id == assignment.ShiftInstanceId && i.CompanyId == companyId);
        if (instance == null)
        {
            swap.Status = RequestStatus.Declined;
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        var shiftType = await _db.ShiftTypes.FindAsync(instance.ShiftTypeId);
        if (shiftType == null)
        {
            swap.Status = RequestStatus.Declined;
            await _db.SaveChangesAsync();
            return RedirectToPage();
        }

        if (!swap.ToUserId.HasValue)
        {
            Error = "Cannot approve an open swap without selecting a recipient.";
            await OnGetAsync();
            return Page();
        }

        var targetUserId = swap.ToUserId.Value;

        var conflict = await _checker.CanAssignAsync(targetUserId, instance);
        if (!conflict.Allowed)
        {
            Error = "Cannot approve swap: " + string.Join(" ", conflict.Reasons);
            await OnGetAsync();
            return Page();
        }

        await using var trx = await _db.Database.BeginTransactionAsync();

        var originalUserId = assignment.UserId;

        assignment.UserId = targetUserId;
        swap.Status = RequestStatus.Approved;

        await _db.SaveChangesAsync();
        await trx.CommitAsync();

        var shiftInfo = $"{shiftType.DisplayName} on {instance.WorkDate:MMM dd, yyyy} " +
                        $"({shiftType.Start:HH:mm} - {shiftType.End:HH:mm})";
        await _notificationService.CreateSwapRequestNotificationAsync(
            originalUserId, RequestStatus.Approved, shiftInfo, swap.Id);

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeclineSwapAsync(int id)
    {
        var companyId = _companyScope.GetCurrentCompanyId(User);
        var swapData = await (from s in _db.SwapRequests
                              where s.Id == id
                              join assign in _db.ShiftAssignments on s.FromAssignmentId equals assign.Id
                              join fromUser in _db.Users on assign.UserId equals fromUser.Id
                              join si in _db.ShiftInstances on assign.ShiftInstanceId equals si.Id
                              join st in _db.ShiftTypes on si.ShiftTypeId equals st.Id
                              where si.CompanyId == companyId
                              select new
                              {
                                  Swap = s,
                                  FromUser = fromUser,
                                  ShiftInfo = $"{st.DisplayName} on {si.WorkDate:MMM dd, yyyy} " +
                                              $"({st.Start:HH:mm} - {st.End:HH:mm})"
                              })
                             .FirstOrDefaultAsync();

        if (swapData == null) return RedirectToPage();
        if (swapData.FromUser.CompanyId != companyId) return Forbid();

        var s = swapData.Swap;
        s.Status = RequestStatus.Declined;
        await _db.SaveChangesAsync();

        await _notificationService.CreateSwapRequestNotificationAsync(
            swapData.FromUser.Id, RequestStatus.Declined, swapData.ShiftInfo, s.Id);

        return RedirectToPage();
    }

    private static string FormatUserName(AppUser user)
        => string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName;
}
