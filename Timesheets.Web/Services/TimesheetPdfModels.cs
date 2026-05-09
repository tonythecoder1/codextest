using Timesheets.Web.Models;

namespace Timesheets.Web.Services;

public class MonthlyTimesheetPdfModel
{
    public string CompanyName { get; set; } = "SAVANA";
    public string Slogan { get; set; } = "BY HUMANS FOR HUMANS";
    public string EmployeeName { get; set; } = string.Empty;
    public string EmployeeEmail { get; set; } = string.Empty;
    public string MonthLabel { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public List<MonthlyTimesheetPdfEntry> Entries { get; set; } = [];
}

public class MonthlyTimesheetPdfEntry
{
    public DateTime WorkDate { get; set; }
    public string TypeLabel { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class TimesheetReportPdfModel
{
    public string CompanyName { get; set; } = "SAVANA";
    public string Slogan { get; set; } = "BY HUMANS FOR HUMANS";
    public string Title { get; set; } = "Relatório de horas";
    public string ScopeLabel { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public int TotalEntries { get; set; }
    public List<TimesheetReportPdfEntry> Entries { get; set; } = [];
}

public class TimesheetReportPdfEntry
{
    public DateTime WorkDate { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string TypeLabel { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public string Description { get; set; } = string.Empty;
    public bool IsBillable { get; set; }
}
