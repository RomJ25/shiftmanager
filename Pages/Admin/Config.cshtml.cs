using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;

namespace ShiftManager.Pages.Admin;

[Authorize(Policy = "IsManagerOrAdmin")]
public class ConfigModel : PageModel
{
    private readonly AppDbContext _db;
    public ConfigModel(AppDbContext db) => _db = db;

    [BindProperty] public int RestHours { get; set; }
    [BindProperty] public int WeeklyCap { get; set; }

    public async Task OnGetAsync()
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        RestHours = GetInt(companyId, "RestHours", 8);
        WeeklyCap = GetInt(companyId, "WeeklyHoursCap", 40);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        await Set(companyId, "RestHours", RestHours.ToString());
        await Set(companyId, "WeeklyHoursCap", WeeklyCap.ToString());
        return RedirectToPage();
    }

    private int GetInt(int companyId, string key, int def)
    {
        var v = _db.Configs.FirstOrDefault(c => c.CompanyId == companyId && c.Key == key)?.Value;
        return int.TryParse(v, out var i) ? i : def;
    }
    private async Task Set(int companyId, string key, string value)
    {
        var c = await _db.Configs.FirstOrDefaultAsync(c => c.CompanyId == companyId && c.Key == key);
        if (c == null) { c = new AppConfig{ CompanyId = companyId, Key = key, Value = value }; _db.Configs.Add(c); }
        else c.Value = value;
        await _db.SaveChangesAsync();
    }
}
