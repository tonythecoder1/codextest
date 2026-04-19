namespace Timesheets.Web.ViewModels;

public class DashboardViewModel
{
    public int ActiveEmployees { get; set; }
    public int ActiveProjects { get; set; }
    public decimal HoursThisWeek { get; set; }
    public decimal HoursThisMonth { get; set; }
    public List<DashboardRecentEntryViewModel> RecentEntries { get; set; } = [];
    public List<EmployeeHoursSummaryViewModel> EmployeeHoursThisMonth { get; set; } = [];
}

public class DashboardRecentEntryViewModel
{
    public DateTime WorkDate { get; set; }
    public string EmployeeName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public decimal Hours { get; set; }
    public bool IsBillable { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class EmployeeHoursSummaryViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal BillableHours { get; set; }
}
