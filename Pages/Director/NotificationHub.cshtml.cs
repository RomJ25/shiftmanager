using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Pages.Director;

[Authorize(Policy = "IsDirector")]
public class NotificationHubModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IDirectorService _directorService;

    public NotificationHubModel(AppDbContext db, IDirectorService directorService)
    {
        _db = db;
        _directorService = directorService;
    }

    public record NotificationVM(
        int Id,
        string Type,
        string Message,
        DateTime CreatedAt,
        bool IsRead,
        string CompanyName,
        string CompanySlug);

    public record PendingRequestVM(
        int Id,
        string Type,
        string UserName,
        string Details,
        DateTime CreatedAt,
        string CompanyName,
        string CompanySlug);

    public List<NotificationVM> RecentNotifications { get; set; } = new();
    public List<PendingRequestVM> PendingTimeOffRequests { get; set; } = new();
    public List<PendingRequestVM> PendingSwapRequests { get; set; } = new();

    public async Task OnGetAsync()
    {
        var companyIds = await _directorService.GetDirectorCompanyIdsAsync();

        if (!companyIds.Any())
            return;

        // Get recent notifications across all assigned companies
        var notificationsQuery = await (from n in _db.UserNotifications
                                        join c in _db.Companies on n.CompanyId equals c.Id
                                        where companyIds.Contains(n.CompanyId)
                                        orderby n.CreatedAt descending
                                        select new NotificationVM(
                                            n.Id,
                                            n.Type.ToString(),
                                            n.Message,
                                            n.CreatedAt,
                                            n.IsRead,
                                            c.Name,
                                            c.Slug ?? ""
                                        ))
                                        .Take(50)
                                        .ToListAsync();
        RecentNotifications = notificationsQuery;

        // Get pending time-off requests
        var timeOffQuery = await (from t in _db.TimeOffRequests
                                  join u in _db.Users on t.UserId equals u.Id
                                  join c in _db.Companies on t.CompanyId equals c.Id
                                  where companyIds.Contains(t.CompanyId) && t.Status == Models.Support.RequestStatus.Pending
                                  orderby t.StartDate
                                  select new PendingRequestVM(
                                      t.Id,
                                      "TimeOff",
                                      u.DisplayName,
                                      $"{t.StartDate:yyyy-MM-dd} to {t.EndDate:yyyy-MM-dd}: {t.Reason}",
                                      t.CreatedAt,
                                      c.Name,
                                      c.Slug ?? ""
                                  ))
                                  .ToListAsync();
        PendingTimeOffRequests = timeOffQuery;

        // Get pending swap requests
        var swapQuery = await (from s in _db.SwapRequests
                               join sa in _db.ShiftAssignments on s.FromAssignmentId equals sa.Id
                               join fromUser in _db.Users on sa.UserId equals fromUser.Id
                               join toUser in _db.Users on s.ToUserId equals toUser.Id
                               join c in _db.Companies on s.CompanyId equals c.Id
                               where companyIds.Contains(s.CompanyId) && s.Status == Models.Support.RequestStatus.Pending
                               orderby s.CreatedAt descending
                               select new PendingRequestVM(
                                   s.Id,
                                   "Swap",
                                   fromUser.DisplayName,
                                   $"Swap with {toUser.DisplayName}",
                                   s.CreatedAt,
                                   c.Name,
                                   c.Slug ?? ""
                               ))
                               .ToListAsync();
        PendingSwapRequests = swapQuery;
    }
}
