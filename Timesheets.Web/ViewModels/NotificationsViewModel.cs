using Timesheets.Web.Models;

namespace Timesheets.Web.ViewModels;

public class NotificationsViewModel
{
    public List<NotificationListItemViewModel> Notifications { get; set; } = [];
}

public class NotificationListItemViewModel
{
    public int Id { get; set; }
    public AppNotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Url { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime? EmailSentAt { get; set; }
    public string? EmailError { get; set; }
}
