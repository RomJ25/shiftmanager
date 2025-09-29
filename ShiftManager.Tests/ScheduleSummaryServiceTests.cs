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
        var shiftType = new ShiftType { CompanyId = company.Id, Key = "NIGHT", Start = new TimeOnly(22, 0), End = new TimeOnly(6, 0) };
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
        var shiftType = new ShiftType { CompanyId = company.Id, Key = "MORNING", Start = new TimeOnly(8, 0), End = new TimeOnly(16, 0) };
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
        var shiftType = new ShiftType { CompanyId = company.Id, Key = "NOON", Start = new TimeOnly(12, 0), End = new TimeOnly(20, 0) };
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
    public async Task QueryScopesShiftTypesToCompany()
    {
        using var context = CreateContext();
        var companyA = await SeedCompanyAsync(context, "Company A");
        var companyB = await SeedCompanyAsync(context, "Company B");

        var date = new DateOnly(2024, 10, 4);
        var shiftTypeA = new ShiftType { CompanyId = companyA.Id, Key = "A", Name = "A Shift", Start = new TimeOnly(6, 0), End = new TimeOnly(14, 0) };
        var shiftTypeB = new ShiftType { CompanyId = companyB.Id, Key = "B", Name = "B Shift", Start = new TimeOnly(7, 0), End = new TimeOnly(15, 0) };
        context.ShiftTypes.AddRange(shiftTypeA, shiftTypeB);
        await context.SaveChangesAsync();

        context.ShiftInstances.AddRange(
            new ShiftInstance
            {
                CompanyId = companyA.Id,
                ShiftTypeId = shiftTypeA.Id,
                WorkDate = date,
                StaffingRequired = 1
            },
            new ShiftInstance
            {
                CompanyId = companyB.Id,
                ShiftTypeId = shiftTypeB.Id,
                WorkDate = date,
                StaffingRequired = 1
            });
        await context.SaveChangesAsync();

        var service = new ScheduleSummaryService(context);
        var result = await service.QueryAsync(new ScheduleSummaryRequest
        {
            CompanyId = companyA.Id,
            StartDate = date,
            EndDate = date,
            ShiftTypeIds = new[] { shiftTypeA.Id, shiftTypeB.Id }
        });

        var shiftType = Assert.Single(result.ShiftTypes);
        Assert.Equal(shiftTypeA.Id, shiftType.Id);
        Assert.Equal(shiftTypeA.Name, shiftType.Name);

        var day = Assert.Single(result.Days);
        var line = Assert.Single(day.Lines);
        Assert.Equal(shiftTypeA.Id, line.ShiftTypeId);
        Assert.Equal(shiftTypeA.Name, line.ShiftTypeName);
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

    private static async Task<Company> SeedCompanyAsync(AppDbContext context, string? name = null)
    {
        var company = new Company { Name = name ?? "Test Co" };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        return company;
    }
}
