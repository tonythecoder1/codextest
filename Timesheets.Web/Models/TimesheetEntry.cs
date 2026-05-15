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

    [Required]
    [Display(Name = "Estado")]
    public TimesheetApprovalStatus ApprovalStatus { get; set; } = TimesheetApprovalStatus.Pending;

    [Display(Name = "Aprovado por")]
    public int? ApprovedByEmployeeId { get; set; }

    [Display(Name = "Aprovado em")]
    public DateTime? ApprovedAt { get; set; }

    [Display(Name = "Projeto")]
    public int? ProjectId { get; set; }

    [Display(Name = "Faturável")]
    public bool IsBillable { get; set; } = true;

    public Employee? Employee { get; set; }
    public Employee? ApprovedByEmployee { get; set; }
    public Project? Project { get; set; }
}
