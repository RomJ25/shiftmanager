using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Pages.Director;

[Authorize(Policy = "IsDirector")]
public class ViewAsModeModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IDirectorService _directorService;
    private readonly IViewAsModeService _viewAsModeService;

    public ViewAsModeModel(
        AppDbContext db,
        IDirectorService directorService,
        IViewAsModeService viewAsModeService)
    {
        _db = db;
        _directorService = directorService;
        _viewAsModeService = viewAsModeService;
    }

    public List<Company> AssignedCompanies { get; set; } = new();
    public bool IsCurrentlyViewing { get; set; }
    public string? CurrentCompanyName { get; set; }

    public async Task OnGetAsync()
    {
        var companyIds = await _directorService.GetDirectorCompanyIdsAsync();
        AssignedCompanies = await _db.Companies
            .Where(c => companyIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync();

        IsCurrentlyViewing = _viewAsModeService.IsViewingAsManager();
        if (IsCurrentlyViewing)
        {
            CurrentCompanyName = await _viewAsModeService.GetViewAsCompanyNameAsync();
        }
    }

    public async Task<IActionResult> OnPostEnterAsync(int companyId)
    {
        var success = await _viewAsModeService.EnterViewAsModeAsync(companyId);

        if (success)
        {
            TempData["SuccessMessage"] = "Now viewing as Manager. Your permissions and visible data are limited to this company.";
            return RedirectToPage("/Calendar/Month");
        }
        else
        {
            TempData["ErrorMessage"] = "Unable to enter View as Manager mode. You may not have access to this company.";
            return RedirectToPage();
        }
    }

    public async Task<IActionResult> OnPostExitAsync()
    {
        await _viewAsModeService.ExitViewAsModeAsync();
        TempData["SuccessMessage"] = "Exited View as Manager mode. Full Director permissions restored.";
        return RedirectToPage();
    }
}
