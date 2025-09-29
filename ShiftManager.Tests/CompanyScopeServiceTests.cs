using Microsoft.EntityFrameworkCore;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Services;
using Xunit;

namespace ShiftManager.Tests;

public class CompanyScopeServiceTests
{
    [Fact]
    public async Task GetCompanySwapRequestAsync_TargetedSwapInCompany_ReturnsRequest()
    {
        await using var context = CreateContext();
        var company = await SeedCompanyAsync(context, "Company A");
        var fromUser = await SeedUserAsync(context, company.Id, "from@test.com", UserRole.Employee);
        var toUser = await SeedUserAsync(context, company.Id, "to@test.com", UserRole.Employee);

        var (_, assignment) = await SeedAssignmentAsync(context, company.Id, fromUser.Id);

        var swapRequest = new SwapRequest
        {
            FromAssignmentId = assignment.Id,
            ToUserId = toUser.Id
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var service = new CompanyScopeService(context);

        var result = await service.GetCompanySwapRequestAsync(swapRequest.Id, company.Id);

        Assert.NotNull(result);
        Assert.Equal(swapRequest.Id, result!.Id);
    }

    [Fact]
    public async Task GetCompanySwapRequestAsync_OpenSwapInCompany_ReturnsRequest()
    {
        await using var context = CreateContext();
        var company = await SeedCompanyAsync(context, "Company A");
        var fromUser = await SeedUserAsync(context, company.Id, "from@test.com", UserRole.Employee);

        var (_, assignment) = await SeedAssignmentAsync(context, company.Id, fromUser.Id);

        var swapRequest = new SwapRequest
        {
            FromAssignmentId = assignment.Id,
            ToUserId = null
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var service = new CompanyScopeService(context);

        var result = await service.GetCompanySwapRequestAsync(swapRequest.Id, company.Id);

        Assert.NotNull(result);
        Assert.Equal(swapRequest.Id, result!.Id);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<Company> SeedCompanyAsync(AppDbContext context, string name)
    {
        var company = new Company { Name = name };
        context.Companies.Add(company);
        await context.SaveChangesAsync();
        return company;
    }

    private static async Task<AppUser> SeedUserAsync(AppDbContext context, int companyId, string email, UserRole role)
    {
        var user = new AppUser
        {
            CompanyId = companyId,
            Email = email,
            DisplayName = email,
            Role = role,
            IsActive = true,
            PasswordHash = Array.Empty<byte>(),
            PasswordSalt = Array.Empty<byte>()
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();
        return user;
    }

    private static async Task<(ShiftInstance instance, ShiftAssignment assignment)> SeedAssignmentAsync(AppDbContext context, int companyId, int userId)
    {
        var shiftType = new ShiftType
        {
            Key = $"KEY-{Guid.NewGuid():N}",
            Name = "Test Shift",
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

        var instance = new ShiftInstance
        {
            CompanyId = companyId,
            ShiftTypeId = shiftType.Id,
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            Name = "Morning"
        };
        context.ShiftInstances.Add(instance);
        await context.SaveChangesAsync();

        var assignment = new ShiftAssignment
        {
            ShiftInstanceId = instance.Id,
            UserId = userId
        };
        context.ShiftAssignments.Add(assignment);
        await context.SaveChangesAsync();

        return (instance, assignment);
    }
}
