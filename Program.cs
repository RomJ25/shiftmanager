using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.Models.Support;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();



builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
});

builder.Services.AddHttpContextAccessor(); // ⬅ add this

builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default"))
       .EnableDetailedErrors()
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment()));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Auth/Login";
        opt.LogoutPath = "/Auth/Logout";
        opt.AccessDeniedPath = "/AccessDenied";
        opt.Cookie.Name = "shiftmgr.auth";
        opt.ExpireTimeSpan = TimeSpan.FromDays(7);
        opt.SlidingExpiration = true;
    });

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("IsManagerOrAdmin",
        policy => policy.RequireAssertion(context =>
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            return UserRoleExtensions.IsManagerial(roleClaim);
        }));

    options.AddPolicy("IsAdmin",
        policy => policy.RequireAssertion(context =>
        {
            var roleClaim = context.User.FindFirst(ClaimTypes.Role)?.Value;
            return Enum.TryParse<UserRole>(roleClaim, out var role) && role == UserRole.Admin;
        }));
});

builder.Services.AddScoped<IConflictChecker, ConflictChecker>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<ICompanyScopeService, CompanyScopeService>();

var app = builder.Build();

// Ensure DB exists and seed minimal data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Seed company
    if (!db.Companies.Any())
    {
        db.Companies.Add(new Company { Name = "Demo Co" });
        await db.SaveChangesAsync();
    }

    var company = db.Companies.First();

    // Seed shift types (fixed keys)
    if (!db.ShiftTypes.Any())
    {
        db.ShiftTypes.AddRange(new[] {
            new ShiftType{ Key="MORNING", Start=new TimeOnly(8,0), End=new TimeOnly(16,0)},
            new ShiftType{ Key="NOON", Start=new TimeOnly(16,0), End=new TimeOnly(0,0)},
            new ShiftType{ Key="NIGHT", Start=new TimeOnly(0,0), End=new TimeOnly(8,0)},
            new ShiftType{ Key="MIDDLE", Start=new TimeOnly(12,0), End=new TimeOnly(20,0)},
        });
        await db.SaveChangesAsync();
    }

    // Seed config
    if (!db.Configs.Any())
    {
        db.Configs.AddRange(new[] {
            new AppConfig{ CompanyId = company.Id, Key = "RestHours", Value = "8" },
            new AppConfig{ CompanyId = company.Id, Key = "WeeklyHoursCap", Value = "40" },
        });
        await db.SaveChangesAsync();
    }

    // Seed admin user
    if (!db.Users.Any())
    {
        var (hash, salt) = PasswordHasher.CreateHash("admin123");
        db.Users.Add(new AppUser
        {
            CompanyId = company.Id,
            Email = "admin@local",
            DisplayName = "Admin",
            Role = UserRole.Admin,
            IsActive = true,
            PasswordHash = hash,
            PasswordSalt = salt
        });
        await db.SaveChangesAsync();
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseStatusCodePages("text/plain", "HTTP {0}");
}
else
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// ✅ Only redirect to HTTPS when explicitly enabled (or in Production)
var enableHttps = app.Configuration.GetValue<bool>("EnableHttpsRedirection", !app.Environment.IsDevelopment());
if (enableHttps)
{
    app.UseHttpsRedirection();
}

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
