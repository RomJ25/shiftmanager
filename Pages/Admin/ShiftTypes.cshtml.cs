using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using System.ComponentModel.DataAnnotations;

namespace ShiftManager.Pages.Admin;

[Authorize(Policy = "IsManagerOrAdmin")]
public class ShiftTypesModel : PageModel
{
    private readonly AppDbContext _db;
    public ShiftTypesModel(AppDbContext db) => _db = db;

    public record Item(int Id, string Key, string Name, string? Description, string Start, string End);
    public List<Item> Items { get; set; } = new();

    [TempData]
    public string? ErrorMessage { get; set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    public async Task OnGetAsync()
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        var t = await _db.ShiftTypes
            .Where(st => st.CompanyId == companyId)
            .OrderBy(s => s.Key)
            .ToListAsync();
        Items = t.Select(x => new Item(x.Id, x.Key, x.Name, x.Description, x.Start.ToString("HH:mm"), x.End.ToString("HH:mm"))).ToList();
    }

    public async Task<IActionResult> OnPostAsync(List<Item> items)
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        // Validate that all items have non-empty names
        if (items.Any(i => string.IsNullOrWhiteSpace(i.Name)))
        {
            ErrorMessage = "כל סוגי המשמרות חייבים להכיל שם"; // All shift types must have a name
            return RedirectToPage();
        }

        // Validate uniqueness of names within the company (excluding the current items being edited)
        var itemNames = items.Select(i => i.Name.Trim()).ToList();
        if (itemNames.Count != itemNames.Distinct().Count())
        {
            ErrorMessage = "שמות סוגי המשמרות חייבים להיות ייחודיים"; // Shift type names must be unique
            return RedirectToPage();
        }

        // Validate uniqueness of keys within the company
        var itemKeys = items.Select(i => i.Key.Trim().ToUpper()).ToList();
        if (itemKeys.Count != itemKeys.Distinct().Count())
        {
            ErrorMessage = "מפתחות סוגי המשמרות חייבים להיות ייחודיים"; // Shift type keys must be unique
            return RedirectToPage();
        }

        var ids = items.Select(i => i.Id).ToList();
        var types = await _db.ShiftTypes
            .Where(s => ids.Contains(s.Id) && s.CompanyId == companyId)
            .ToListAsync();

        foreach (var it in items)
        {
            var t = types.First(x => x.Id == it.Id);
            t.Name = it.Name.Trim();
            t.Description = string.IsNullOrWhiteSpace(it.Description) ? null : it.Description.Trim();
            t.Key = it.Key.Trim().ToUpper();
            t.Start = TimeOnly.Parse(it.Start);
            t.End = TimeOnly.Parse(it.End);
        }

        await _db.SaveChangesAsync();
        SuccessMessage = "סוגי המשמרות עודכנו בהצלחה"; // Shift types updated successfully
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddAsync(string key, string name, string? description, string start, string end)
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);

        // Validate input
        if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(start) || string.IsNullOrWhiteSpace(end))
        {
            ErrorMessage = "כל השדות החובה חייבים להיות מלאים"; // All required fields must be filled
            return RedirectToPage();
        }

        key = key.Trim().ToUpper();
        name = name.Trim();

        // Check if key already exists for this company
        var existingKey = await _db.ShiftTypes
            .AnyAsync(st => st.CompanyId == companyId && st.Key == key);
        if (existingKey)
        {
            ErrorMessage = $"מפתח '{key}' כבר קיים"; // Key already exists
            return RedirectToPage();
        }

        // Check if name already exists for this company
        var existingName = await _db.ShiftTypes
            .AnyAsync(st => st.CompanyId == companyId && st.Name == name);
        if (existingName)
        {
            ErrorMessage = $"שם '{name}' כבר קיים"; // Name already exists
            return RedirectToPage();
        }

        // Create new shift type
        var newShiftType = new ShiftType
        {
            CompanyId = companyId,
            Key = key,
            Name = name,
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Start = TimeOnly.Parse(start),
            End = TimeOnly.Parse(end)
        };

        _db.ShiftTypes.Add(newShiftType);
        await _db.SaveChangesAsync();

        SuccessMessage = $"סוג משמרת '{name}' נוסף בהצלחה"; // Shift type added successfully
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var companyId = int.Parse(User.FindFirst("CompanyId")!.Value);
        var shiftType = await _db.ShiftTypes
            .FirstOrDefaultAsync(st => st.Id == id && st.CompanyId == companyId);

        if (shiftType == null)
        {
            ErrorMessage = "סוג משמרת לא נמצא"; // Shift type not found
            return RedirectToPage();
        }

        // Check if this shift type is in use
        var inUse = await _db.ShiftInstances.AnyAsync(si => si.ShiftTypeId == id);
        if (inUse)
        {
            ErrorMessage = $"לא ניתן למחוק את '{shiftType.Name}' כי הוא בשימוש"; // Cannot delete because it's in use
            return RedirectToPage();
        }

        _db.ShiftTypes.Remove(shiftType);
        await _db.SaveChangesAsync();

        SuccessMessage = $"סוג משמרת '{shiftType.Name}' נמחק בהצלחה"; // Shift type deleted successfully
        return RedirectToPage();
    }
}
