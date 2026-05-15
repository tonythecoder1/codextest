using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class NotificationsController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var employeeId = GetCurrentEmployeeId();
        var notifications = await context.AppNotifications
            .AsNoTracking()
            .Where(notification => notification.EmployeeId == employeeId)
            .OrderByDescending(notification => notification.CreatedAt)
            .Take(50)
            .Select(notification => new NotificationListItemViewModel
            {
                Id = notification.Id,
                Type = notification.Type,
                Title = notification.Title,
                Message = notification.Message,
                Url = notification.Url,
                CreatedAt = notification.CreatedAt,
                ReadAt = notification.ReadAt,
                EmailSentAt = notification.EmailSentAt,
                EmailError = notification.EmailError
            })
            .ToListAsync();

        return View(new NotificationsViewModel { Notifications = notifications });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id)
    {
        var employeeId = GetCurrentEmployeeId();
        var notification = await context.AppNotifications
            .FirstOrDefaultAsync(item => item.Id == id && item.EmployeeId == employeeId);

        if (notification is null)
        {
            return NotFound();
        }

        notification.ReadAt ??= DateTime.UtcNow;
        await context.SaveChangesAsync();

        if (IsAjaxRequest())
        {
            return NoContent();
        }

        if (!string.IsNullOrWhiteSpace(notification.Url))
        {
            return Redirect(notification.Url);
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var employeeId = GetCurrentEmployeeId();
        var notifications = await context.AppNotifications
            .Where(item => item.EmployeeId == employeeId && item.ReadAt == null)
            .ToListAsync();

        foreach (var notification in notifications)
        {
            notification.ReadAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync();
        if (IsAjaxRequest())
        {
            return NoContent();
        }

        return RedirectToAction(nameof(Index));
    }

    private bool IsAjaxRequest()
    {
        return string.Equals(Request.Headers.XRequestedWith, "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);
    }

    private int GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var employeeId) ? employeeId : 0;
    }
}
