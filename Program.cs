using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.Models.Support;
using System.Globalization;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Localization
builder.Services.AddLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { new CultureInfo("en-US"), new CultureInfo("he-IL") };
    options.DefaultRequestCulture = new RequestCulture("en-US"); // Use English as default/fallback
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;
    options.FallBackToParentCultures = true;
    options.FallBackToParentUICultures = true;

    // Use cookie-based culture provider (persists across requests) and query string (for switching)
    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();

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
        policy => policy.RequireRole(nameof(UserRole.Manager), nameof(UserRole.Admin)));
    options.AddPolicy("IsAdmin", policy => policy.RequireRole(nameof(UserRole.Admin)));
});

builder.Services.AddScoped<IConflictChecker, ConflictChecker>();
builder.Services.AddScoped<INotificationService, NotificationService>();

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

    // Seed shift types per company (using Keys for localization)
    if (!db.ShiftTypes.Any(st => st.CompanyId == company.Id))
    {
        db.ShiftTypes.AddRange(new[] {
            new ShiftType{
                CompanyId = company.Id,
                Key = "MORNING",
                Name = "MORNING",
                Description = null,
                Start = new TimeOnly(8,0),
                End = new TimeOnly(16,0)
            },
            new ShiftType{
                CompanyId = company.Id,
                Key = "NOON",
                Name = "NOON",
                Description = null,
                Start = new TimeOnly(16,0),
                End = new TimeOnly(0,0)
            },
            new ShiftType{
                CompanyId = company.Id,
                Key = "NIGHT",
                Name = "NIGHT",
                Description = null,
                Start = new TimeOnly(0,0),
                End = new TimeOnly(8,0)
            },
            new ShiftType{
                CompanyId = company.Id,
                Key = "MIDDLE",
                Name = "MIDDLE",
                Description = null,
                Start = new TimeOnly(12,0),
                End = new TimeOnly(20,0)
            },
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

// Use the configured localization options
var localizationOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

// Middleware to persist culture selection via cookie when query string is used
app.Use(async (context, next) =>
{
    if (context.Request.Query.ContainsKey("culture"))
    {
        var culture = context.Request.Query["culture"].ToString();
        var uiCulture = context.Request.Query["uiculture"].ToString();

        context.Response.Cookies.Append(
            CookieRequestCultureProvider.DefaultCookieName,
            CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture, uiCulture)),
            new CookieOptions { Expires = DateTimeOffset.UtcNow.AddYears(1) }
        );
    }
    await next();
});

app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.Run();
