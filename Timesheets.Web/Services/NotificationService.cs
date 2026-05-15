using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;

namespace Timesheets.Web.Services;

public class NotificationService(AppDbContext context, IEmailSender emailSender) : INotificationService
{
    public async Task<AppNotification> NotifyAsync(
        int employeeId,
        AppNotificationType type,
        string title,
        string message,
        string? url = null,
        bool sendEmail = false,
        string? emailSubject = null)
    {
        var notification = new AppNotification
        {
            EmployeeId = employeeId,
            Type = type,
            Title = title,
            Message = message,
            Url = url,
            CreatedAt = DateTime.UtcNow
        };

        context.AppNotifications.Add(notification);

        if (sendEmail)
        {
            var employee = await context.Employees
                .AsNoTracking()
                .FirstOrDefaultAsync(item => item.Id == employeeId);

            if (employee is not null)
            {
                var result = await emailSender.SendAsync(new EmailMessage
                {
                    ToEmail = employee.Email,
                    ToName = employee.FullName,
                    Subject = emailSubject ?? title,
                    Body = message
                });

                if (result.Sent)
                {
                    notification.EmailSentAt = DateTime.UtcNow;
                }
                else
                {
                    notification.EmailError = result.Error;
                }
            }
        }

        await context.SaveChangesAsync();
        return notification;
    }
}
