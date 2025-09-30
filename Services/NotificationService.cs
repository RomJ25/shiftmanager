using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using ShiftManager.Data;
using ShiftManager.Models;
using ShiftManager.Models.Support;

namespace ShiftManager.Services;

public interface INotificationService
{
    Task CreateNotificationAsync(int userId, NotificationType type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null);
    Task CreateShiftAddedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime);
    Task CreateShiftRemovedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime);
    Task CreateTimeOffNotificationAsync(int userId, RequestStatus status, DateOnly startDate, DateOnly endDate, int requestId);
    Task CreateSwapRequestNotificationAsync(int userId, RequestStatus status, string shiftInfo, int requestId);
}

public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;
    private readonly ILogger<NotificationService> _logger;
    private readonly IStringLocalizer<Resources.SharedResources> _localizer;

    public NotificationService(AppDbContext db, ILogger<NotificationService> logger, IStringLocalizer<Resources.SharedResources> localizer)
    {
        _db = db;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task CreateNotificationAsync(int userId, NotificationType type, string title, string message, int? relatedEntityId = null, string? relatedEntityType = null)
    {
        try
        {
            var notification = new UserNotification
            {
                UserId = userId,
                Type = type,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = DateTime.UtcNow,
                RelatedEntityId = relatedEntityId,
                RelatedEntityType = relatedEntityType
            };

            _db.UserNotifications.Add(notification);
            await _db.SaveChangesAsync();

            _logger.LogInformation("Created notification {Type} for user {UserId}: {Title}", type, userId, title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating notification {Type} for user {UserId}", type, userId);
        }
    }

    public async Task CreateShiftAddedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime)
    {
        var title = _localizer["NewShiftAssignment"];
        var message = string.Format(_localizer["ShiftAssignedMessage"], shiftTypeName, shiftDate, startTime, endTime);

        await CreateNotificationAsync(userId, NotificationType.ShiftAdded, title, message, null, "ShiftAssignment");
    }

    public async Task CreateShiftRemovedNotificationAsync(int userId, string shiftTypeName, DateOnly shiftDate, TimeOnly startTime, TimeOnly endTime)
    {
        var title = _localizer["ShiftAssignmentRemoved"];
        var message = string.Format(_localizer["ShiftRemovedMessage"], shiftTypeName, shiftDate, startTime, endTime);

        await CreateNotificationAsync(userId, NotificationType.ShiftRemoved, title, message, null, "ShiftAssignment");
    }

    public async Task CreateTimeOffNotificationAsync(int userId, RequestStatus status, DateOnly startDate, DateOnly endDate, int requestId)
    {
        var statusText = status == RequestStatus.Approved ? _localizer["Approved"] : _localizer["Declined"];
        var title = status == RequestStatus.Approved ? _localizer["TimeOffRequestApproved"] : _localizer["TimeOffRequestDeclined"];
        var dateRange = startDate == endDate ? startDate.ToString("dd/MM/yyyy") : $"{startDate:dd/MM} - {endDate:dd/MM/yyyy}";
        var message = status == RequestStatus.Approved
            ? string.Format(_localizer["TimeOffApprovedMessage"], dateRange)
            : string.Format(_localizer["TimeOffDeclinedMessage"], dateRange);

        var notificationType = status == RequestStatus.Approved ? NotificationType.TimeOffApproved : NotificationType.TimeOffDeclined;

        await CreateNotificationAsync(userId, notificationType, title, message, requestId, "TimeOffRequest");
    }

    public async Task CreateSwapRequestNotificationAsync(int userId, RequestStatus status, string shiftInfo, int requestId)
    {
        var title = status == RequestStatus.Approved ? _localizer["ShiftSwapRequestApproved"] : _localizer["ShiftSwapRequestDeclined"];
        var message = status == RequestStatus.Approved
            ? string.Format(_localizer["ShiftSwapApprovedMessage"], shiftInfo)
            : string.Format(_localizer["ShiftSwapDeclinedMessage"], shiftInfo);

        var notificationType = status == RequestStatus.Approved ? NotificationType.SwapRequestApproved : NotificationType.SwapRequestDeclined;

        await CreateNotificationAsync(userId, notificationType, title, message, requestId, "SwapRequest");
    }
}