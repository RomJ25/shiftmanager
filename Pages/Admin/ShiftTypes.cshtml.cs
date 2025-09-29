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

    public record Item(int Id, string Key, string Name, string Start, string End);
    public List<Item> Items { get; set; } = new();

    public async Task OnGetAsync()
    {
        var t = await _db.ShiftTypes.OrderBy(s => s.Key).ToListAsync();
        Items = t
            .Select(x => new Item(
                x.Id,
                x.Key,
                string.IsNullOrWhiteSpace(x.Name) ? x.DefaultName : x.Name,
                x.Start.ToString("HH:mm"),
                x.End.ToString("HH:mm")))
            .ToList();
    }

    public async Task<IActionResult> OnPostAsync(List<Item> items)
    {
        var ids = items.Select(i => i.Id).ToList();
        var types = await _db.ShiftTypes.Where(s => ids.Contains(s.Id)).ToListAsync();
        foreach (var it in items)
        {
            var t = types.First(x => x.Id == it.Id);
            t.Key = it.Key;
            t.Name = string.IsNullOrWhiteSpace(it.Name) ? string.Empty : it.Name.Trim();
            t.Start = TimeOnly.Parse(it.Start);
            t.End = TimeOnly.Parse(it.End);
        }
        await _db.SaveChangesAsync();
        return RedirectToPage();
    }
}
