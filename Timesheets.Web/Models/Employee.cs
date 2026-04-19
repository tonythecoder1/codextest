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

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Display(Name = "Administrador")]
    public bool IsAdmin { get; set; }

    [NotMapped]
    [DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string? Password { get; set; }

    public ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
}
