using System.Linq;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Services;
using Xunit;

namespace ShiftManager.Tests;

public class ScheduleSummaryServiceTests
{
    [Fact]
    public async Task WrapAroundShiftPreservesTimes()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = await SeedShiftTypeAsync(context, company, "NIGHT", new TimeOnly(22, 0), new TimeOnly(6, 0));
        var otherShiftType = await SeedOtherCompanyShiftTypeAsync(context);

        var instance = new ShiftInstance
        {
            CompanyId = company.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = new DateOnly(2024, 10, 1),
            StaffingRequired = 1,
            Name = "Overnight"
        };
        context.ShiftInstances.Add(instance);
        await context.SaveChangesAsync();

        var service = new ScheduleSummaryService(context);
        var result = await service.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = company.Id,
            StartDate = instance.WorkDate,
            EndDate = instance.WorkDate
        });

        var line = Assert.Single(Assert.Single(result.Days).Lines.Where(l => l.ShiftTypeId == shiftType.Id));
        Assert.Equal(new TimeOnly(22, 0), line.StartTime);
        Assert.Equal(new TimeOnly(6, 0), line.EndTime);
        Assert.DoesNotContain(result.Days.SelectMany(d => d.Lines), l => l.ShiftTypeId == otherShiftType.Id);
    }

    [Fact]
    public async Task StaffingCountsReflectAssignments()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = await SeedShiftTypeAsync(context, company, "MORNING", new TimeOnly(8, 0), new TimeOnly(16, 0));
        var otherShiftType = await SeedOtherCompanyShiftTypeAsync(context);

        var instance = new ShiftInstance
        {
            CompanyId = company.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = new DateOnly(2024, 10, 2),
            StaffingRequired = 3
        };
        context.ShiftInstances.Add(instance);
        await context.SaveChangesAsync();

        var alice = new AppUser { CompanyId = company.Id, Email = "alice@test", DisplayName = "Alice" };
        var bob = new AppUser { CompanyId = company.Id, Email = "bob@test", DisplayName = "Bob" };
        context.Users.AddRange(alice, bob);
        await context.SaveChangesAsync();

        context.ShiftAssignments.AddRange(
            new ShiftAssignment { ShiftInstanceId = instance.Id, UserId = alice.Id },
            new ShiftAssignment { ShiftInstanceId = instance.Id, UserId = bob.Id });
        await context.SaveChangesAsync();

        var service = new ScheduleSummaryService(context);
        var result = await service.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = company.Id,
            StartDate = instance.WorkDate,
            EndDate = instance.WorkDate
        });

        var line = Assert.Single(Assert.Single(result.Days).Lines.Where(l => l.ShiftTypeId == shiftType.Id));
        Assert.Equal(2, line.Assigned);
        Assert.Equal(3, line.Required);
        Assert.Contains("Alice", line.AssignedNames);
        Assert.Contains("Bob", line.AssignedNames);
        Assert.DoesNotContain(result.Days.SelectMany(d => d.Lines), l => l.ShiftTypeId == otherShiftType.Id);
    }

    [Fact]
    public async Task EmptySlotsMatchRequiredMinusAssigned()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = await SeedShiftTypeAsync(context, company, "NOON", new TimeOnly(12, 0), new TimeOnly(20, 0));
        var otherShiftType = await SeedOtherCompanyShiftTypeAsync(context);

        var instance = new ShiftInstance
        {
            CompanyId = company.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = new DateOnly(2024, 10, 3),
            StaffingRequired = 4
        };
        context.ShiftInstances.Add(instance);
        await context.SaveChangesAsync();

        var user = new AppUser { CompanyId = company.Id, Email = "carol@test", DisplayName = "Carol" };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        context.ShiftAssignments.Add(new ShiftAssignment { ShiftInstanceId = instance.Id, UserId = user.Id });
        await context.SaveChangesAsync();

        var service = new ScheduleSummaryService(context);
        var result = await service.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = company.Id,
            StartDate = instance.WorkDate,
            EndDate = instance.WorkDate
        });

        var line = Assert.Single(Assert.Single(result.Days).Lines.Where(l => l.ShiftTypeId == shiftType.Id));
        Assert.Equal(3, line.EmptySlots.Count);
        Assert.All(line.EmptySlots, slot => Assert.Equal("Empty", slot));
        Assert.DoesNotContain(result.Days.SelectMany(d => d.Lines), l => l.ShiftTypeId == otherShiftType.Id);
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        using (var pragma = connection.CreateCommand())
        {
            pragma.CommandText = "PRAGMA foreign_keys = ON;";
            pragma.ExecuteNonQuery();
        }

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .EnableDetailedErrors()
            .EnableSensitiveDataLogging()
            .Options;

        var context = new TestAppDbContext(options, connection);
        context.Database.EnsureCreated();
        context.ChangeTracker.AutoDetectChangesEnabled = true;
        context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.TrackAll;
        return context;
    }

    private sealed class TestAppDbContext : AppDbContext
    {
        private readonly SqliteConnection _connection;

        public TestAppDbContext(DbContextOptions<AppDbContext> options, SqliteConnection connection)
            : base(options)
        {
            _connection = connection;
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                _connection.Dispose();
            }
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private static async Task<Company> SeedCompanyAsync(AppDbContext context, string name = "Test Co")
    {
        var company = new Company { Name = name };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        return company;
    }

    private static async Task<ShiftType> SeedShiftTypeAsync(AppDbContext context, Company company, string key, TimeOnly start, TimeOnly end)
    {
        var shiftType = new ShiftType
        {
            CompanyId = company.Id,
            Key = key,
            Start = start,
            End = end
        };

        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();
        return shiftType;
    }

    private static async Task<ShiftType> SeedOtherCompanyShiftTypeAsync(AppDbContext context)
    {
        var otherCompany = await SeedCompanyAsync(context, "Other Co");
        return await SeedShiftTypeAsync(context, otherCompany, "OTHER", new TimeOnly(1, 0), new TimeOnly(9, 0));
    }
}
