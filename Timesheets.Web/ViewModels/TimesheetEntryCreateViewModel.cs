using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using Timesheets.Web.Models;

namespace Timesheets.Web.ViewModels;

public class TimesheetEntryCreateViewModel
{
    public bool CanChooseEmployee { get; set; }
    public List<ExistingCalendarEntryViewModel> ExistingEntries { get; set; } = [];

    [Required]
    [Range(1, int.MaxValue, ErrorMessage = "Seleciona um colaborador.")]
    [Display(Name = "Colaborador")]
    public int EmployeeId { get; set; }

    [Display(Name = "Registos")]
    public string EntriesJson { get; set; } = string.Empty;

    public List<SelectListItem> Employees { get; set; } = [];
    public List<ProjectCalendarOptionViewModel> Projects { get; set; } = [];
    public List<SelectListItem> EntryTypes { get; set; } = [];
}

public class ProjectCalendarOptionViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ClientName { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#0F766E";
    public bool AllowWeekendWork { get; set; }
}

public class TimesheetEntryCreateInput
{
    public DateTime WorkDate { get; set; }
    public TimesheetEntryType EntryType { get; set; } = TimesheetEntryType.Work;
    public int? ProjectId { get; set; }
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsBillable { get; set; } = true;
    public int? EmployeeId { get; set; }
}

public class ExistingCalendarEntryViewModel
{
    public int Id { get; set; }
    public int EmployeeId { get; set; }
    public DateTime WorkDate { get; set; }
    public TimesheetEntryType EntryType { get; set; }
    public int? ProjectId { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectColorHex { get; set; } = "#6B8760";
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
}
