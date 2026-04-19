using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.Models;

public class Project
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Projeto")]
    [StringLength(120)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Cliente")]
    [StringLength(120)]
    public string ClientName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Cor")]
    [StringLength(7)]
    public string ColorHex { get; set; } = "#0F766E";

    [Display(Name = "Ativo")]
    public bool IsActive { get; set; } = true;

    public ICollection<TimesheetEntry> TimesheetEntries { get; set; } = new List<TimesheetEntry>();
}
