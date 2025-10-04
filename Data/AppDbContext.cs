using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;

namespace ShiftManager.Data;

public class AppDbContext : DbContext
{
    private readonly ITenantResolver? _tenantResolver;

    public AppDbContext(DbContextOptions<AppDbContext> options, ITenantResolver? tenantResolver = null)
        : base(options)
    {
        _tenantResolver = tenantResolver;
    }

    public DbSet<Company> Companies => Set<Company>();
    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<ShiftType> ShiftTypes => Set<ShiftType>();
    public DbSet<ShiftInstance> ShiftInstances => Set<ShiftInstance>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<TimeOffRequest> TimeOffRequests => Set<TimeOffRequest>();
    public DbSet<SwapRequest> SwapRequests => Set<SwapRequest>();
    public DbSet<UserNotification> UserNotifications => Set<UserNotification>();
    public DbSet<AppConfig> Configs => Set<AppConfig>();
    public DbSet<DirectorCompany> DirectorCompanies => Set<DirectorCompany>();
    public DbSet<UserJoinRequest> UserJoinRequests => Set<UserJoinRequest>();
    public DbSet<RoleAssignmentAudit> RoleAssignmentAudits => Set<RoleAssignmentAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var dateConverter = new ValueConverter<DateOnly, string>(
            v => v.ToString("yyyy-MM-dd"),
            v => DateOnly.Parse(v));

        var timeConverter = new ValueConverter<TimeOnly, string>(
            v => v.ToString("HH:mm"),
            v => TimeOnly.Parse(v));

        modelBuilder.Entity<ShiftType>()
            .Property(p => p.Start).HasConversion(timeConverter);
        modelBuilder.Entity<ShiftType>()
            .Property(p => p.End).HasConversion(timeConverter);

        modelBuilder.Entity<ShiftInstance>()
            .Property(p => p.WorkDate).HasConversion(dateConverter);

        modelBuilder.Entity<TimeOffRequest>()
            .Property(p => p.StartDate).HasConversion(dateConverter);
        modelBuilder.Entity<TimeOffRequest>()
            .Property(p => p.EndDate).HasConversion(dateConverter);

        modelBuilder.Entity<ShiftInstance>()
            .Property(p => p.Concurrency).IsConcurrencyToken();

        // Multitenancy Phase 1: Add unique index on Company.Slug for routing
        modelBuilder.Entity<Company>()
            .HasIndex(c => c.Slug).IsUnique();

        modelBuilder.Entity<AppUser>()
            .HasIndex(u => u.Email).IsUnique();

        // Multitenancy: Add composite index for ShiftType (CompanyId, Key)
        modelBuilder.Entity<ShiftType>()
            .HasIndex(s => new { s.CompanyId, s.Key });

        // Multitenancy Phase 1: Update ShiftAssignment index to include CompanyId
        modelBuilder.Entity<ShiftAssignment>()
            .HasIndex(a => new { a.CompanyId, a.ShiftInstanceId, a.UserId }).IsUnique();

        // Multitenancy Phase 1: Add composite index for TimeOffRequests
        modelBuilder.Entity<TimeOffRequest>()
            .HasIndex(t => new { t.CompanyId, t.UserId, t.StartDate });

        // Multitenancy Phase 1: Add composite index for SwapRequests
        modelBuilder.Entity<SwapRequest>()
            .HasIndex(s => new { s.CompanyId, s.Status, s.CreatedAt });

        // Multitenancy Phase 1: Update UserNotification index to include CompanyId
        modelBuilder.Entity<UserNotification>()
            .HasIndex(n => new { n.CompanyId, n.UserId, n.CreatedAt });

        // Director role: Configure DirectorCompany mappings
        modelBuilder.Entity<DirectorCompany>()
            .HasIndex(dc => new { dc.UserId, dc.CompanyId })
            .IsUnique()
            .HasFilter("[IsDeleted] = 0"); // Unique only for active records

        modelBuilder.Entity<DirectorCompany>()
            .HasIndex(dc => dc.CompanyId); // For querying directors of a company

        modelBuilder.Entity<DirectorCompany>()
            .HasIndex(dc => dc.UserId); // For querying companies of a director

        modelBuilder.Entity<DirectorCompany>()
            .HasOne(dc => dc.User)
            .WithMany()
            .HasForeignKey(dc => dc.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectorCompany>()
            .HasOne(dc => dc.Company)
            .WithMany()
            .HasForeignKey(dc => dc.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<DirectorCompany>()
            .HasOne(dc => dc.GrantedByUser)
            .WithMany()
            .HasForeignKey(dc => dc.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure UserJoinRequest
        modelBuilder.Entity<UserJoinRequest>()
            .HasIndex(jr => new { jr.Email, jr.CompanyId, jr.Status });

        modelBuilder.Entity<UserJoinRequest>()
            .HasIndex(jr => new { jr.CompanyId, jr.Status, jr.CreatedAt });

        modelBuilder.Entity<UserJoinRequest>()
            .HasOne(jr => jr.Company)
            .WithMany()
            .HasForeignKey(jr => jr.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserJoinRequest>()
            .HasOne(jr => jr.ReviewedByUser)
            .WithMany()
            .HasForeignKey(jr => jr.ReviewedBy)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<UserJoinRequest>()
            .HasOne(jr => jr.CreatedUser)
            .WithMany()
            .HasForeignKey(jr => jr.CreatedUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Configure RoleAssignmentAudit
        modelBuilder.Entity<RoleAssignmentAudit>()
            .HasIndex(ra => new { ra.CompanyId, ra.TargetUserId, ra.Timestamp });

        modelBuilder.Entity<RoleAssignmentAudit>()
            .HasIndex(ra => new { ra.ChangedBy, ra.Timestamp });

        // Multitenancy Phase 2: Global query filters for automatic tenant scoping
        if (_tenantResolver != null)
        {
            modelBuilder.Entity<ShiftType>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());

            modelBuilder.Entity<ShiftInstance>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());

            modelBuilder.Entity<ShiftAssignment>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());

            modelBuilder.Entity<TimeOffRequest>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());

            modelBuilder.Entity<SwapRequest>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());

            modelBuilder.Entity<UserNotification>()
                .HasQueryFilter(e => e.CompanyId == _tenantResolver.GetCurrentTenantId());
        }

        base.OnModelCreating(modelBuilder);
    }
}
