using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Timesheets.Web.ViewModels;

public class TimesheetEntryFormViewModel
{
    public int? Id { get; set; }

    [Required]
    [Display(Name = "Colaborador")]
    public int EmployeeId { get; set; }

    [Required]
    [Display(Name = "Projeto")]
    public int ProjectId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Data")]
    public DateTime WorkDate { get; set; } = DateTime.Today;

    [Required]
    [Range(0.25, 24)]
    [Display(Name = "Horas")]
    public decimal Hours { get; set; }

    [Required]
    [StringLength(280)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Faturável")]
    public bool IsBillable { get; set; } = true;

    public List<SelectListItem> Employees { get; set; } = [];
    public List<SelectListItem> Projects { get; set; } = [];
}
