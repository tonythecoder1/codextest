using Microsoft.AspNetCore.Mvc.Rendering;
using Timesheets.Web.Models;

namespace Timesheets.Web.ViewModels;

public class TimesheetEntriesIndexViewModel
{
    public bool CanChooseEmployee { get; set; }
    public int? EmployeeId { get; set; }
    public int? ProjectId { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public decimal TotalHours { get; set; }
    public int EntryCount { get; set; }
    public string SelectedMonth { get; set; } = string.Empty;
    public bool CanGenerateMonthlyPdf { get; set; }
    public string MonthlyPdfHelpText { get; set; } = string.Empty;
    public List<SelectListItem> CompletedMonths { get; set; } = [];
    public List<SelectListItem> Employees { get; set; } = [];
    public List<SelectListItem> Projects { get; set; } = [];
    public List<TimesheetEntryListItemViewModel> Entries { get; set; } = [];
}

public class TimesheetEntryListItemViewModel
{
    public int Id { get; set; }
    public DateTime WorkDate { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectColorHex { get; set; } = "#0F766E";
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }
    public TimesheetEntryType EntryType { get; set; }
    public string Description { get; set; } = string.Empty;
}
