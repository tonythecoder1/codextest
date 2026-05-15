using Timesheets.Web.Models;

namespace Timesheets.Web.Services;

public interface INotificationService
{
    Task<AppNotification> NotifyAsync(
        int employeeId,
        AppNotificationType type,
        string title,
        string message,
        string? url = null,
        bool sendEmail = false,
        string? emailSubject = null);
}
