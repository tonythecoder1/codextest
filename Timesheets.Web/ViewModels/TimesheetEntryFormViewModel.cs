using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace Timesheets.Web.ViewModels;

public class TimesheetEntryFormViewModel
{
    public int? Id { get; set; }

    public bool CanChooseEmployee { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleciona um colaborador.")]
    [Display(Name = "Colaborador")]
    public int EmployeeId { get; set; }

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleciona um projeto.")]
    [Display(Name = "Projeto")]
    public int ProjectId { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "Data")]
    public DateTime WorkDate { get; set; } = DateTime.Today;

    [Required]
    [Range(0.25, 12)]
    [Display(Name = "Horas")]
    public decimal Hours { get; set; }

    [Required]
    [StringLength(280)]
    [Display(Name = "Descrição")]
    public string Description { get; set; } = string.Empty;

    [Display(Name = "Faturável")]
    public bool IsBillable { get; set; } = true;

    [Display(Name = "Dias selecionados")]
    public string SelectedDates { get; set; } = string.Empty;

    public List<SelectListItem> Employees { get; set; } = [];
    public List<SelectListItem> Projects { get; set; } = [];
}
