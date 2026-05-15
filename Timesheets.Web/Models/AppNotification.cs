using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.Models;

public class AppNotification
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [Required]
    [StringLength(40)]
    public AppNotificationType Type { get; set; }

    [Required]
    [StringLength(140)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    [StringLength(300)]
    public string? Url { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }
    public DateTime? EmailSentAt { get; set; }

    [StringLength(500)]
    public string? EmailError { get; set; }

    public Employee? Employee { get; set; }
}
