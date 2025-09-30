using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace ShiftManager.Pages;

public class AccessDeniedModel : PageModel
{
    public string ReturnUrl { get; set; } = "/Calendar/Month";

    public IActionResult OnGet(string? returnUrl = null)
    {
        // Use the referring page or default to calendar
        ReturnUrl = returnUrl ?? Request.Headers["Referer"].FirstOrDefault() ?? "/Calendar/Month";

        // If the return URL contains admin or assignments paths, default to calendar
        if (ReturnUrl.Contains("/Admin/") || ReturnUrl.Contains("/Assignments/"))
        {
            ReturnUrl = "/Calendar/Month";
        }

        return Page();
    }
}