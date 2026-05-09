using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.Models;

public class TimesheetEntry
{
    public int Id { get; set; }

    [Required]
    [Display(Name = "Tipo")]
    public TimesheetEntryType EntryType { get; set; } = TimesheetEntryType.Work;

    [Required]
    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime WorkDate { get; set; } = DateTime.Today;

    [Required]
    [Range(0.25, 12)]
    [Display(Name = "Horas")]
    public decimal Hours { get; set; }

    [Required]
    [Display(Name = "Descrição")]
    [StringLength(280)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [Display(Name = "Colaborador")]
    public int EmployeeId { get; set; }

    [Display(Name = "Projeto")]
    public int? ProjectId { get; set; }

    [Display(Name = "Faturável")]
    public bool IsBillable { get; set; } = true;

    public Employee? Employee { get; set; }
    public Project? Project { get; set; }
}
