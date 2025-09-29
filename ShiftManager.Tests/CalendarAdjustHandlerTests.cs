using System;
using System.Collections.Generic;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Pages.Calendar;
using ShiftManager.Services;
using Xunit;

namespace ShiftManager.Tests;

public class CalendarAdjustHandlerTests
{
    [Fact]
    public async Task DayAdjust_ForeignShiftType_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);

        var foreignShiftType = await SeedShiftTypeAsync(context, companyB.Id);

        var model = new DayModel(context, NullLogger<DayModel>.Instance, new ScheduleSummaryService(context));
        AttachUser(model, companyId: companyA.Id);

        var payload = new DayModel.AdjustPayload
        {
            date = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            shiftTypeId = foreignShiftType.Id,
            delta = 1,
            concurrency = 0
        };

        var result = await model.OnPostAdjustAsync(payload);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.ShiftInstances);
    }

    [Fact]
    public async Task WeekAdjust_ForeignShiftType_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);

        var foreignShiftType = await SeedShiftTypeAsync(context, companyB.Id);

        var model = new WeekModel(context, NullLogger<WeekModel>.Instance, new ScheduleSummaryService(context));
        AttachUser(model, companyId: companyA.Id);

        var payload = new WeekModel.AdjustPayload
        {
            date = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            shiftTypeId = foreignShiftType.Id,
            delta = 1,
            concurrency = 0
        };

        var result = await model.OnPostAdjustAsync(payload);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.ShiftInstances);
    }

    [Fact]
    public async Task MonthAdjust_ForeignShiftType_IsRejected()
    {
        await using var context = CreateContext();
        var (companyA, companyB) = await SeedCompaniesAsync(context);

        var foreignShiftType = await SeedShiftTypeAsync(context, companyB.Id);

        var model = new MonthModel(context, NullLogger<MonthModel>.Instance, new ScheduleSummaryService(context));
        AttachUser(model, companyId: companyA.Id);

        var payload = new MonthModel.AdjustPayload
        {
            date = DateOnly.FromDateTime(DateTime.Today).ToString("yyyy-MM-dd"),
            shiftTypeId = foreignShiftType.Id,
            delta = 1,
            concurrency = 0
        };

        var result = await model.OnPostAdjustAsync(payload);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(context.ShiftInstances);
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

    private static async Task<ShiftType> SeedShiftTypeAsync(AppDbContext context, int companyId)
    {
        var shiftType = new ShiftType
        {
            CompanyId = companyId,
            Key = "FOREIGN",
            Name = "Foreign",
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        };
        context.ShiftTypes.Add(shiftType);
        await context.SaveChangesAsync();
        return shiftType;
    }

    private static void AttachUser(PageModel model, int companyId)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, "1"),
            new("CompanyId", companyId.ToString())
        };

        var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var httpContext = new DefaultHttpContext { User = principal };
        model.PageContext = new PageContext
        {
            HttpContext = httpContext
        };
    }
}
