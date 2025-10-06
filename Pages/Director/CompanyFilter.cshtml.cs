using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Pages.Director;

[Authorize(Policy = "IsDirector")]
public class CompanyFilterModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly IDirectorService _directorService;
    private readonly ICompanyFilterService _filterService;

    public CompanyFilterModel(
        AppDbContext db,
        IDirectorService directorService,
        ICompanyFilterService filterService)
    {
        _db = db;
        _directorService = directorService;
        _filterService = filterService;
    }

    public List<Company> AssignedCompanies { get; set; } = new();
    public List<int> SelectedCompanyIds { get; set; } = new();

    [BindProperty] public List<int> CompanyIds { get; set; } = new();

    public async Task OnGetAsync()
    {
        var companyIds = await _directorService.GetDirectorCompanyIdsAsync();
        AssignedCompanies = await _db.Companies
            .Where(c => companyIds.Contains(c.Id))
            .OrderBy(c => c.Name)
            .ToListAsync();

        SelectedCompanyIds = await _filterService.GetSelectedCompanyIdsAsync();
    }

    public async Task<IActionResult> OnPostSetFilterAsync()
    {
        await _filterService.SetSelectedCompanyIdsAsync(CompanyIds);
        TempData["SuccessMessage"] = "Company filter updated successfully.";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostClearFilterAsync()
    {
        await _filterService.ClearFilterAsync();
        TempData["SuccessMessage"] = "Company filter cleared. Showing all companies.";
        return RedirectToPage();
    }
}
