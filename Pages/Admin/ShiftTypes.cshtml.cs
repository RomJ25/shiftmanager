using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;

namespace ShiftManager.Pages.Admin;

[Authorize(Policy = "IsManagerOrAdmin")]
public class ShiftTypesModel : PageModel
{
    private readonly AppDbContext _db;
    public ShiftTypesModel(AppDbContext db) => _db = db;

    public record Item(int Id, string Key, string Name, string Start, string End, int CompanyId);
    public List<Item> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        var t = await _db.ShiftTypes
            .Where(s => s.CompanyId == companyId)
            .OrderBy(s => s.Key)
            .ToListAsync();

        Items = t.Select(x => new Item(
                x.Id,
                x.Key,
                string.IsNullOrWhiteSpace(x.Name) ? x.DefaultName : x.Name,
                x.Start.ToString("HH:mm"),
                x.End.ToString("HH:mm"),
                x.CompanyId))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(List<Item> items)
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        var ids = items.Where(i => i.Id != 0).Select(i => i.Id).ToList();
        var types = await _db.ShiftTypes
            .Where(s => ids.Contains(s.Id) && s.CompanyId == companyId)
            .ToListAsync();

        foreach (var it in items)
        {
            if (it.Id == 0)
            {
                _db.ShiftTypes.Add(new ShiftType
                {
                    CompanyId = companyId,
                    Key = it.Key,
                    Start = TimeOnly.Parse(it.Start),
                    End = TimeOnly.Parse(it.End)
                });
                continue;
            }

            if (it.CompanyId != companyId)
            {
                continue;
            }

            var t = types.FirstOrDefault(x => x.Id == it.Id);
            if (t is null)
            {
                continue;
            }

            t.Key = it.Key;
            t.Name = string.IsNullOrWhiteSpace(it.Name) ? string.Empty : it.Name.Trim();
            t.Start = TimeOnly.Parse(it.Start);
            t.End = TimeOnly.Parse(it.End);
        }

        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
