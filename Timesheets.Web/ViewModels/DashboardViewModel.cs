using Timesheets.Web.Models;

namespace Timesheets.Web.ViewModels;

public class DashboardViewModel
{
    public bool IsAdmin { get; set; }
    public bool IsManager { get; set; }
    public string PrimaryStatLabel { get; set; } = "Colaboradores ativos";
    public string SecondaryStatLabel { get; set; } = "Projetos ativos";
    public string SummarySectionTitle { get; set; } = "Horas por colaborador";
    public string SummaryEyebrow { get; set; } = "Capacidade";
    public int ActiveEmployees { get; set; }
    public int ActiveProjects { get; set; }
    public decimal HoursThisWeek { get; set; }
    public decimal HoursThisMonth { get; set; }
    public int PendingApprovals { get; set; }
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
    public TimesheetApprovalStatus ApprovalStatus { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class EmployeeHoursSummaryViewModel
{
    public string EmployeeName { get; set; } = string.Empty;
    public decimal TotalHours { get; set; }
    public decimal BillableHours { get; set; }
}
