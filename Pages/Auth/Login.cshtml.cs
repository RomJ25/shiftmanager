using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace ShiftManager.Pages.Auth;

public class LoginModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<LoginModel> _logger;
    public LoginModel(AppDbContext db, ILogger<LoginModel> logger) { _db = db; _logger = logger; }

    [BindProperty] public string Email { get; set; } = string.Empty;
    [BindProperty] public string Password { get; set; } = string.Empty;
    public string? Error { get; set; }

    public void OnGet() { }

    public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
    {
        try
        {
            var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == Email && u.IsActive);
            if (user == null || !PasswordHasher.Verify(Password, user.PasswordHash, user.PasswordSalt))
            {
                _logger.LogWarning("Login failed for {Email} (user not found or bad password).", Email);
                Error = "Invalid credentials.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.DisplayName),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("CompanyId", user.CompanyId.ToString())
            };
            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(identity));

            _logger.LogInformation("User {UserId} ({Email}) signed in. Role={Role}", user.Id, user.Email, user.Role);

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToPage("/Calendar/Month");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception during login for {Email}.", Email);
            Error = "Unexpected error. Please try again.";
            return Page();
        }
    }
}
