using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.Models;

public class PasswordResetToken
{
    public int Id { get; set; }

    [Required]
    public int EmployeeId { get; set; }

    [Required]
    [StringLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? UsedAt { get; set; }
    public int? CreatedByEmployeeId { get; set; }

    public Employee? Employee { get; set; }
    public Employee? CreatedByEmployee { get; set; }
}
