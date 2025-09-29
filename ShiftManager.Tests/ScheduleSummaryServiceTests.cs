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
        var shiftType = new ShiftType { Key = "NIGHT", Start = new TimeOnly(22, 0), End = new TimeOnly(6, 0) };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

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
    }

    [Fact]
    public async Task StaffingCountsReflectAssignments()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = new ShiftType { Key = "MORNING", Start = new TimeOnly(8, 0), End = new TimeOnly(16, 0) };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

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
    }

    [Fact]
    public async Task EmptySlotsMatchRequiredMinusAssigned()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = new ShiftType { Key = "NOON", Start = new TimeOnly(12, 0), End = new TimeOnly(20, 0) };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

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
    }

    [Fact]
    public async Task UsesDisplayNameWhenNameMissing()
    {
        using var context = CreateContext();
        var company = await SeedCompanyAsync(context);
        var shiftType = new ShiftType { Key = "MORNING", Name = string.Empty, Start = new TimeOnly(8, 0), End = new TimeOnly(16, 0) };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

        var instance = new ShiftInstance
        {
            CompanyId = company.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = new DateOnly(2024, 10, 4),
            StaffingRequired = 1
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

        var displayName = "Morning Shift";

        var shiftTypeSummary = Assert.Single(result.ShiftTypes);
        Assert.Equal(displayName, shiftTypeSummary.Name);
        Assert.Equal("Mor", shiftTypeSummary.ShortName);

        var line = Assert.Single(Assert.Single(result.Days).Lines);
        Assert.Equal(displayName, line.ShiftTypeName);
        Assert.Equal("Mor", line.ShiftTypeShortName);
    }

    private static AppDbContext CreateContext()
    {
        var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new TestAppDbContext(options, connection);
        context.Database.EnsureCreated();
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

    private static async Task<Company> SeedCompanyAsync(AppDbContext context)
    {
        var company = new Company { Name = "Test Co" };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        return company;
    }
}
