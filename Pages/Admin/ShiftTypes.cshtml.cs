using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;

namespace ShiftManager.Pages.Admin;

[Authorize(Policy = "IsManagerOrAdmin")]
public class ShiftTypesModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ICompanyContext _companyContext;

    public ShiftTypesModel(AppDbContext db, ICompanyContext companyContext)
    {
        _db = db;
        _companyContext = companyContext;
    }

    public record Item(int Id, string Key, string Name, string Start, string End);
    public List<Item> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        var companyId = _companyContext.GetCompanyIdOrThrow();
        // Query filter automatically scopes ShiftTypes by CompanyId
        var t = await _db.ShiftTypes.OrderBy(s => s.Key).ToListAsync();
        Items = t.Select(x => new Item(x.Id, x.Key, x.Name, x.Start.ToString("HH:mm"), x.End.ToString("HH:mm"))).ToList();
    }

    public async Task<IActionResult> OnPostAsync(List<Item> items)
    {
        var companyId = _companyContext.GetCompanyIdOrThrow();
        var ids = items.Select(i => i.Id).ToList();
        // Query filter ensures we only get ShiftTypes for current company
        var types = await _db.ShiftTypes.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var it in items)
        {
            var t = types.First(x => x.Id == it.Id);
            t.Key = it.Key;
            t.Start = TimeOnly.Parse(it.Start);
            t.End = TimeOnly.Parse(it.End);
        }
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
