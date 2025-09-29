using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;
using ShiftManager.Pages.Admin;
using ShiftManager.Pages.Requests;
using ShiftManager.Services;
using Xunit;

namespace ShiftManager.Tests;

public class CompanyScopeGuardTests
{
    [Fact]
    public async Task ApproveTimeOff_FromDifferentCompany_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);
        var admin = await SeedUserAsync(context, companyA.Id, "admin@a.test", UserRole.Admin);
        var otherUser = await SeedUserAsync(context, companyB.Id, "employee@b.test", UserRole.Employee);

        var request = new TimeOffRequest
        {
            UserId = otherUser.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Today),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Reason = "Vacation"
        };
        context.TimeOffRequests.Add(request);
        await context.SaveChangesAsync();

        var companyScope = new CompanyScopeService(context);
        var page = new IndexModel(context, new ConflictChecker(context), new StubNotificationService(), NullLogger<IndexModel>.Instance, companyScope);
        AttachUser(page, admin.Id, companyA.Id);

        var result = await page.OnPostApproveTimeOffAsync(request.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(RequestStatus.Pending, (await context.TimeOffRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task ApproveSwap_FromDifferentCompany_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);
        var admin = await SeedUserAsync(context, companyA.Id, "admin@a.test", UserRole.Admin);
        var fromUser = await SeedUserAsync(context, companyB.Id, "from@b.test", UserRole.Employee);
        var toUser = await SeedUserAsync(context, companyB.Id, "to@b.test", UserRole.Employee);

        var shiftType = new ShiftType { Key = "TEST", Start = new TimeOnly(8, 0), End = new TimeOnly(16, 0) };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

        var instance = new ShiftInstance
        {
            CompanyId = companyB.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = DateOnly.FromDateTime(DateTime.Today),
            Name = "Morning"
        };
        context.ShiftInstances.Add(instance);
        await context.SaveChangesAsync();

        var assignment = new ShiftAssignment
        {
            ShiftInstanceId = instance.Id,
            UserId = fromUser.Id
        };
        context.ShiftAssignments.Add(assignment);
        await context.SaveChangesAsync();

        var swapRequest = new SwapRequest
        {
            FromAssignmentId = assignment.Id,
            ToUserId = toUser.Id
        };
        context.SwapRequests.Add(swapRequest);
        await context.SaveChangesAsync();

        var companyScope = new CompanyScopeService(context);
        var page = new IndexModel(context, new ConflictChecker(context), new StubNotificationService(), NullLogger<IndexModel>.Instance, companyScope);
        AttachUser(page, admin.Id, companyA.Id);

        var result = await page.OnPostApproveSwapAsync(swapRequest.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.Equal(RequestStatus.Pending, (await context.SwapRequests.SingleAsync()).Status);
    }

    [Fact]
    public async Task IndexModel_Handlers_ExecuteSuccessfully()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);
        var admin = await SeedUserAsync(context, companyA.Id, "admin@a.test", UserRole.Admin);
        var employee = await SeedUserAsync(context, companyA.Id, "employee@a.test", UserRole.Employee);
        var recipient = await SeedUserAsync(context, companyA.Id, "recipient@a.test", UserRole.Employee);
        await SeedUserAsync(context, companyB.Id, "other@b.test", UserRole.Employee);

        var approveTimeOff = new TimeOffRequest
        {
            UserId = employee.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(1)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(2)),
            Reason = "Vacation"
        };
        var declineTimeOff = new TimeOffRequest
        {
            UserId = employee.Id,
            StartDate = DateOnly.FromDateTime(DateTime.Today.AddDays(10)),
            EndDate = DateOnly.FromDateTime(DateTime.Today.AddDays(12)),
            Reason = "Personal"
        };
        context.TimeOffRequests.AddRange(approveTimeOff, declineTimeOff);

        var shiftType = new ShiftType
        {
            CompanyId = companyA.Id,
            Key = "DAY",
            Name = "Day Shift",
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(17, 0)
        };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();

        var instanceApprove = new ShiftInstance
        {
            CompanyId = companyA.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = DateOnly.FromDateTime(DateTime.Today.AddDays(3)),
            Name = "Day"
        };
        var instanceDecline = new ShiftInstance
        {
            CompanyId = companyA.Id,
            ShiftTypeId = shiftType.Id,
            WorkDate = DateOnly.FromDateTime(DateTime.Today.AddDays(4)),
            Name = "Day"
        };
        context.ShiftInstances.AddRange(instanceApprove, instanceDecline);
        await context.SaveChangesAsync();

        var assignmentApprove = new ShiftAssignment { ShiftInstanceId = instanceApprove.Id, UserId = employee.Id };
        var assignmentDecline = new ShiftAssignment { ShiftInstanceId = instanceDecline.Id, UserId = employee.Id };
        context.ShiftAssignments.AddRange(assignmentApprove, assignmentDecline);
        await context.SaveChangesAsync();

        var approveSwap = new SwapRequest { FromAssignmentId = assignmentApprove.Id, ToUserId = recipient.Id };
        var declineSwap = new SwapRequest { FromAssignmentId = assignmentDecline.Id, ToUserId = recipient.Id };
        context.SwapRequests.AddRange(approveSwap, declineSwap);
        await context.SaveChangesAsync();

        var notifications = new StubNotificationService();
        var companyScope = new CompanyScopeService(context);
        var checker = new AllowingConflictChecker();
        var page = new IndexModel(context, checker, notifications, NullLogger<IndexModel>.Instance, companyScope);
        AttachUser(page, admin.Id, companyA.Id);

        await page.OnGetAsync();

        Assert.Equal(2, page.TimeOff.Count);
        Assert.Equal(2, page.Swaps.Count);

        var approveTimeOffResult = await page.OnPostApproveTimeOffAsync(approveTimeOff.Id);
        Assert.IsType<RedirectToPageResult>(approveTimeOffResult);
        Assert.Equal(RequestStatus.Approved, (await context.TimeOffRequests.FindAsync(approveTimeOff.Id))!.Status);
        Assert.Contains(notifications.TimeOffNotifications, n => n.requestId == approveTimeOff.Id && n.status == RequestStatus.Approved);

        var declineTimeOffResult = await page.OnPostDeclineTimeOffAsync(declineTimeOff.Id);
        Assert.IsType<RedirectToPageResult>(declineTimeOffResult);
        Assert.Equal(RequestStatus.Declined, (await context.TimeOffRequests.FindAsync(declineTimeOff.Id))!.Status);
        Assert.Contains(notifications.TimeOffNotifications, n => n.requestId == declineTimeOff.Id && n.status == RequestStatus.Declined);

        var approveSwapResult = await page.OnPostApproveSwapAsync(approveSwap.Id);
        Assert.IsType<RedirectToPageResult>(approveSwapResult);
        Assert.Equal(RequestStatus.Approved, (await context.SwapRequests.FindAsync(approveSwap.Id))!.Status);
        Assert.Equal(recipient.Id, (await context.ShiftAssignments.FindAsync(assignmentApprove.Id))!.UserId);
        Assert.Contains(notifications.SwapNotifications, n => n.requestId == approveSwap.Id && n.status == RequestStatus.Approved);

        var declineSwapResult = await page.OnPostDeclineSwapAsync(declineSwap.Id);
        Assert.IsType<RedirectToPageResult>(declineSwapResult);
        Assert.Equal(RequestStatus.Declined, (await context.SwapRequests.FindAsync(declineSwap.Id))!.Status);
        Assert.Contains(notifications.SwapNotifications, n => n.requestId == declineSwap.Id && n.status == RequestStatus.Declined);
    }

    [Fact]
    public async Task ToggleUser_FromDifferentCompany_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);
        var admin = await SeedUserAsync(context, companyA.Id, "admin@a.test", UserRole.Admin);
        var otherUser = await SeedUserAsync(context, companyB.Id, "employee@b.test", UserRole.Employee);

        var companyScope = new CompanyScopeService(context);
        var page = new UsersModel(context, NullLogger<UsersModel>.Instance, companyScope);
        AttachUser(page, admin.Id, companyA.Id);

        var result = await page.OnPostToggleAsync(otherUser.Id);

        Assert.IsType<ForbidResult>(result);
        Assert.True((await context.Users.SingleAsync(u => u.Id == otherUser.Id)).IsActive);
    }

    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static async Task<(Company companyA, Company companyB)> SeedCompaniesAsync(AppDbContext context)
    {
        var companyA = new Company { Name = "Company A" };
        var companyB = new Company { Name = "Company B" };
        context.Companies.AddRange(companyA, companyB);
        await context.SaveChangesAsync();
        return (companyA, companyB);
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

    private static void AttachUser(PageModel model, int userId, int companyId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new("CompanyId", companyId.ToString()),
            new(ClaimTypes.Role, nameof(UserRole.Admin))
        };
        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext { User = principal };
        model.PageContext = new PageContext
        {
            HttpContext = httpContext
        };
    }

    private sealed class StubNotificationService : INotificationService
    {
        public List<(int userId, RequestStatus status, DateOnly startDate, DateOnly endDate, int requestId)> TimeOffNotifications { get; } = new();
        public List<(int userId, RequestStatus status, string shiftInfo, int requestId)> SwapNotifications { get; } = new();

        public Task CreateNotificationAsync(int userId, NotificationType type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
            => Task.CompletedTask;

        public Task CreateShiftAddedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime)
            => Task.CompletedTask;

        public Task CreateShiftRemovedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime)
            => Task.CompletedTask;

        public Task CreateTimeOffNotificationAsync(int userId, RequestStatus status, DateOnly startDate, DateOnly endDate, int requestId)
        {
            TimeOffNotifications.Add((userId, status, startDate, endDate, requestId));
            return Task.CompletedTask;
        }

        public Task CreateSwapRequestNotificationAsync(int userId, RequestStatus status, string shiftInfo, int requestId)
        {
            SwapNotifications.Add((userId, status, shiftInfo, requestId));
            return Task.CompletedTask;
        }
    }

    private sealed class AllowingConflictChecker : IConflictChecker
    {
        public Task<ConflictResult> CanAssignAsync(int userId, ShiftInstance instance, CancellationToken ct = default)
            => Task.FromResult(ConflictResult.Ok());
    }
}
