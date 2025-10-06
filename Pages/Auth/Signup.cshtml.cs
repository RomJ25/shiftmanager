using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using System.ComponentModel.DataAnnotations;

namespace ShiftManager.Pages.Auth;

[AllowAnonymous]
public class SignupModel : PageModel
{
    private readonly AppDbContext _db;
    private readonly ILogger<SignupModel> _logger;

    public SignupModel(AppDbContext db, ILogger<SignupModel> logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty, Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [BindProperty, Required]
    public string DisplayName { get; set; } = string.Empty;

    [BindProperty, Required, MinLength(6)]
    public string Password { get; set; } = string.Empty;

    [BindProperty, Required]
    public int CompanyId { get; set; }

    [BindProperty, Required]
    public UserRole RequestedRole { get; set; } = UserRole.Employee;

    public List<Company> AvailableCompanies { get; set; } = new();
    public string? Error { get; set; }
    public string? PendingRequestMessage { get; set; }

    public async Task OnGetAsync()
    {
        AvailableCompanies = await _db.Companies
            .OrderBy(c => c.Name)
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        // Load companies for form redisplay if needed
        AvailableCompanies = await _db.Companies
            .OrderBy(c => c.Name)
            .ToListAsync();

        if (!ModelState.IsValid)
        {
            Error = "Please fill in all required fields.";
            return Page();
        }

        // Check if user already exists
        if (await _db.Users.AnyAsync(u => u.Email == Email))
        {
            Error = "An account with this email already exists. Please login instead.";
            return Page();
        }

        // Check for existing pending request with same email, company, and role
        var existingPendingRequest = await _db.UserJoinRequests
            .Include(jr => jr.Company)
            .FirstOrDefaultAsync(jr =>
                jr.Email == Email &&
                jr.CompanyId == CompanyId &&
                jr.RequestedRole == RequestedRole &&
                jr.Status == JoinRequestStatus.Pending);

        if (existingPendingRequest != null)
        {
            var company = await _db.Companies.FindAsync(CompanyId);
            PendingRequestMessage = $"Your request to join {company?.Name} as {RequestedRole} is under review. We'll notify you once it's approved.";
            return Page();
        }

        // Validate company exists
        var selectedCompany = await _db.Companies.FindAsync(CompanyId);
        if (selectedCompany == null)
        {
            Error = "Selected company not found.";
            return Page();
        }

        // Create password hash
        var (hash, salt) = PasswordHasher.CreateHash(Password);

        // Create join request
        var joinRequest = new UserJoinRequest
        {
            Email = Email,
            DisplayName = DisplayName,
            PasswordHash = hash,
            PasswordSalt = salt,
            CompanyId = CompanyId,
            RequestedRole = RequestedRole,
            Status = JoinRequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        _db.UserJoinRequests.Add(joinRequest);
        await _db.SaveChangesAsync();

        _logger.LogInformation("New join request created: {Email} requesting {Role} at {Company}",
            Email, RequestedRole, selectedCompany.Name);

        PendingRequestMessage = $"Your request to join {selectedCompany.Name} as {RequestedRole} has been submitted. We'll notify you once it's reviewed.";

        // Clear form fields
        Email = string.Empty;
        DisplayName = string.Empty;
        Password = string.Empty;
        CompanyId = 0;
        RequestedRole = UserRole.Employee;

        return Page();
    }
}
