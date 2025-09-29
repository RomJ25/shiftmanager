using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;

namespace ShiftManager.Pages.Requests.Swaps;

[Authorize]
public class CreateModel : PageModel
{
    private readonly AppDbContext _db;
    public CreateModel(AppDbContext db) => _db = db;

    public record AssignmentVM(int AssignmentId, string Label);
    public List<AssignmentVM> MyAssignments { get; set; } = new();
    public List<AppUser> OtherUsers { get; set; } = new();

    [BindProperty] public int? SelectedAssignmentId { get; set; }
    [BindProperty] public int? ToUserId { get; set; }

    public async Task OnGetAsync()
    {
        int userId = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
        var upcoming = await (from a in _db.ShiftAssignments
                              join si in _db.ShiftInstances on a.ShiftInstanceId equals si.Id
                              join st in _db.ShiftTypes on si.ShiftTypeId equals st.Id
                              where a.UserId == userId && si.WorkDate >= DateOnly.FromDateTime(DateTime.Today)
                              orderby si.WorkDate
                              select new AssignmentVM(a.Id, $"{si.WorkDate:yyyy-MM-dd} {st.Key}")).ToListAsync();
        MyAssignments = upcoming;

        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        OtherUsers = await _db.Users.Where(u => u.CompanyId == companyId && u.IsActive && u.Id != userId).OrderBy(u => u.DisplayName).ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await OnGetAsync();

        if (SelectedAssignmentId is null)
        {
            ModelState.AddModelError(nameof(SelectedAssignmentId), "Please choose one of your upcoming shifts.");
            return Page();
        }

        if (ToUserId.HasValue && !OtherUsers.Any(u => u.Id == ToUserId.Value))
        {
            ModelState.AddModelError(nameof(ToUserId), "Please select a valid teammate or leave the request open.");
            return Page();
        }

        _db.SwapRequests.Add(new SwapRequest { FromAssignmentId = SelectedAssignmentId.Value, ToUserId = ToUserId });
        await _db.SaveChangesAsync();
        return RedirectToPage("/Requests/Index");
    }
}
