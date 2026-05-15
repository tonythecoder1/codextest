using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Timesheets.Web.Models;

public class Employee
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Nome")]
    [StringLength(120)]
    public string FullName { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Email")]
    [StringLength(160)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Função")]
    [StringLength(120)]
    public string JobTitle { get; set; } = string.Empty;

    [Display(Name = "Ativo")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Responsável")]
    public int? ResponsibleId { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Display(Name = "Administrador")]
    public bool IsAdmin { get; set; }

    [Display(Name = "Responsável")]
    public bool IsManager { get; set; }

    [NotMapped]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public Employee? Responsible { get; set; }
    public ICollection<Employee> ManagedEmployees { get; set; } = new List<Employee>();
    public ICollection<Project> ManagedProjects { get; set; } = new List<Project>();
    public ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
    public ICollection<AppNotification> Notifications { get; set; } = new List<AppNotification>();
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
}
