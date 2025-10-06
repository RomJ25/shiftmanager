using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using ShiftManager.Models.Support;
using Microsoft.AspNetCore.Localization;
using System.Globalization;
using ShiftManager.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();



// Configure localization
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[] { "en-US", "he-IL" };
    options.DefaultRequestCulture = new RequestCulture("en-US");
    options.SupportedCultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();
    options.SupportedUICultures = supportedCultures.Select(c => new CultureInfo(c)).ToList();

    options.RequestCultureProviders.Clear();
    options.RequestCultureProviders.Add(new QueryStringRequestCultureProvider());
    options.RequestCultureProviders.Add(new CookieRequestCultureProvider());
    options.RequestCultureProviders.Add(new AcceptLanguageHeaderRequestCultureProvider());
});

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/");
    options.Conventions.AllowAnonymousToPage("/Auth/Login");
    options.Conventions.AllowAnonymousToPage("/Auth/Signup");
})
.AddViewLocalization()
.AddDataAnnotationsLocalization();

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ILocalizationService, LocalizationService>();

// Multitenancy Phase 2: Register tenant resolver and company context
builder.Services.AddScoped<ITenantResolver, TenantResolver>();
builder.Services.AddScoped<ICompanyContext, CompanyContext>();

// Multitenancy Phase 2: Register CompanyId interceptor
builder.Services.AddSingleton<CompanyIdInterceptor>();

builder.Services.AddDbContext<AppDbContext>((serviceProvider, opt) =>
{
    var interceptor = serviceProvider.GetRequiredService<CompanyIdInterceptor>();
    opt.UseSqlite(builder.Configuration.GetConnectionString("Default"))
       .EnableDetailedErrors()
       .EnableSensitiveDataLogging(builder.Environment.IsDevelopment())
       .AddInterceptors(interceptor);
});

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
        policy => policy.RequireRole(nameof(UserRole.Manager), nameof(UserRole.Owner), nameof(UserRole.Director)));
    options.AddPolicy("IsAdmin", policy => policy.RequireRole(nameof(UserRole.Owner)));
    options.AddPolicy("IsDirector", policy => policy.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Director)));
    options.AddPolicy("IsOwnerOrDirector", policy => policy.RequireRole(nameof(UserRole.Owner), nameof(UserRole.Director)));
});

builder.Services.AddScoped<IConflictChecker, ConflictChecker>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IDirectorService, DirectorService>();
builder.Services.AddScoped<ITraineeService, TraineeService>();
builder.Services.AddScoped<ICompanyFilterService, CompanyFilterService>();
builder.Services.AddScoped<IViewAsModeService, ViewAsModeService>();

// Add health checks for container orchestration
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

var app = builder.Build();

// Ensure DB exists and seed minimal data
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    // Get seed passwords from environment (required in production)
    var seedAdminPassword = app.Configuration["SEED_ADMIN_PASSWORD"] ?? Environment.GetEnvironmentVariable("SEED_ADMIN_PASSWORD");
    var seedDirectorPassword = app.Configuration["SEED_DIRECTOR_PASSWORD"] ?? Environment.GetEnvironmentVariable("SEED_DIRECTOR_PASSWORD");

    // In production, require explicit seed passwords
    if (!app.Environment.IsDevelopment())
    {
        if (string.IsNullOrEmpty(seedAdminPassword))
        {
            throw new InvalidOperationException(
                "SEED_ADMIN_PASSWORD environment variable must be set in production. " +
                "Set this via environment variables or app configuration.");
        }
    }

    // Use defaults only in development
    seedAdminPassword ??= "admin123";
    seedDirectorPassword ??= "director123";

    // Seed company
    if (!db.Companies.Any())
    {
        db.Companies.Add(new Company { Name = "Demo Co" });
        await db.SaveChangesAsync();
    }

    var company = db.Companies.First();

    // Seed shift types (fixed keys) - company-specific
    if (!db.ShiftTypes.Any())
    {
        db.ShiftTypes.AddRange(new[] {
            new ShiftType{ CompanyId=company.Id, Key="MORNING", Start=new TimeOnly(8,0), End=new TimeOnly(16,0)},
            new ShiftType{ CompanyId=company.Id, Key="NOON", Start=new TimeOnly(16,0), End=new TimeOnly(0,0)},
            new ShiftType{ CompanyId=company.Id, Key="NIGHT", Start=new TimeOnly(0,0), End=new TimeOnly(8,0)},
            new ShiftType{ CompanyId=company.Id, Key="MIDDLE", Start=new TimeOnly(12,0), End=new TimeOnly(20,0)},
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

    // Seed owner user
    if (!db.Users.Any())
    {
        var (hash, salt) = PasswordHasher.CreateHash(seedAdminPassword);
        db.Users.Add(new AppUser
        {
            CompanyId = company.Id,
            Email = "admin@local",
            DisplayName = "Owner",
            Role = UserRole.Owner,
            IsActive = true,
            PasswordHash = hash,
            PasswordSalt = salt
        });
        await db.SaveChangesAsync();
    }

    // Seed test Director user and companies (for QA)
    var enableDirectorRole = app.Configuration.GetValue<bool>("Features:EnableDirectorRole", false);
    if (enableDirectorRole && app.Environment.IsDevelopment())
    {
        // Create second company if it doesn't exist
        if (db.Companies.Count() < 2)
        {
            var company2 = new Company { Name = "Test Corp", Slug = "test-corp", DisplayName = "Test Corporation" };
            db.Companies.Add(company2);
            await db.SaveChangesAsync();

            // Seed shift types for second company
            db.ShiftTypes.AddRange(new[] {
                new ShiftType{ CompanyId=company2.Id, Key="MORNING", Start=new TimeOnly(8,0), End=new TimeOnly(16,0)},
                new ShiftType{ CompanyId=company2.Id, Key="NOON", Start=new TimeOnly(16,0), End=new TimeOnly(0,0)},
                new ShiftType{ CompanyId=company2.Id, Key="NIGHT", Start=new TimeOnly(0,0), End=new TimeOnly(8,0)},
                new ShiftType{ CompanyId=company2.Id, Key="MIDDLE", Start=new TimeOnly(12,0), End=new TimeOnly(20,0)},
            });
            await db.SaveChangesAsync();

            // Seed config for second company
            db.Configs.AddRange(new[] {
                new AppConfig{ CompanyId = company2.Id, Key = "RestHours", Value = "8" },
                new AppConfig{ CompanyId = company2.Id, Key = "WeeklyHoursCap", Value = "40" },
            });
            await db.SaveChangesAsync();
        }

        // Create Director user if doesn't exist
        if (!db.Users.Any(u => u.Role == UserRole.Director))
        {
            var (dirHash, dirSalt) = PasswordHasher.CreateHash(seedDirectorPassword);
            var director = new AppUser
            {
                CompanyId = company.Id,
                Email = "director@local",
                DisplayName = "Test Director",
                Role = UserRole.Director,
                IsActive = true,
                PasswordHash = dirHash,
                PasswordSalt = dirSalt
            };
            db.Users.Add(director);
            await db.SaveChangesAsync();

            // Assign Director to both companies
            var ownerUser = db.Users.First(u => u.Role == UserRole.Owner);
            var allCompanies = db.Companies.ToList();

            foreach (var comp in allCompanies)
            {
                if (!db.DirectorCompanies.Any(dc => dc.UserId == director.Id && dc.CompanyId == comp.Id))
                {
                    db.DirectorCompanies.Add(new DirectorCompany
                    {
                        UserId = director.Id,
                        CompanyId = comp.Id,
                        GrantedBy = ownerUser.Id,
                        GrantedAt = DateTime.UtcNow
                    });
                }
            }
            await db.SaveChangesAsync();
        }
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

// Add request localization middleware
app.UseRequestLocalization();

// Multitenancy Phase 2: Add company context middleware
app.UseMiddleware<CompanyContextMiddleware>();

app.UseAuthentication();
app.UseAuthorization();
app.MapRazorPages();
app.MapHealthChecks("/health");
app.Run();
