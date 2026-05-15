using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Security;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class HomeController(ILogger<HomeController> logger, AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isManager = !isAdmin && User.IsInRole(AppRoles.Manager);
        var currentEmployeeId = GetCurrentEmployeeId();
        var today = DateTime.Today;
        var daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var startOfWeek = today.AddDays(-daysSinceMonday);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var managedEmployeeIds = isManager
            ? await context.Employees
                .AsNoTracking()
                .Where(employee => employee.ResponsibleId == currentEmployeeId)
                .Select(employee => employee.Id)
                .ToListAsync()
            : [];
        var managedProjectIds = isManager
            ? await context.Projects
                .AsNoTracking()
                .Where(project => project.ResponsibleEmployees.Any(employee => employee.Id == currentEmployeeId))
                .Select(project => project.Id)
                .ToListAsync()
            : [];

        var timesheetEntries = context.TimesheetEntries.AsNoTracking().AsQueryable();

        if (isManager)
        {
            timesheetEntries = timesheetEntries.Where(entry =>
                entry.EmployeeId == currentEmployeeId ||
                managedEmployeeIds.Contains(entry.EmployeeId) ||
                (entry.ProjectId.HasValue && managedProjectIds.Contains(entry.ProjectId.Value)));
        }
        else if (!isAdmin)
        {
            timesheetEntries = timesheetEntries.Where(entry => entry.EmployeeId == currentEmployeeId);
        }

        var model = new DashboardViewModel
        {
            IsAdmin = isAdmin,
            IsManager = isManager,
            PrimaryStatLabel = isAdmin ? "Colaboradores ativos" : isManager ? "Colaboradores a cargo" : "A minha equipa",
            SecondaryStatLabel = isAdmin ? "Projetos ativos" : isManager ? "Projetos a cargo" : "Os meus projetos",
            SummaryEyebrow = isAdmin ? "Capacidade" : isManager ? "Equipa" : "Resumo",
            SummarySectionTitle = isAdmin ? "Horas por colaborador" : isManager ? "Horas da equipa" : "As minhas horas no mês",
            ActiveEmployees = isAdmin
                ? await context.Employees.CountAsync(employee => employee.IsActive)
                : isManager
                    ? managedEmployeeIds.Count
                    : 1,
            ActiveProjects = isAdmin
                ? await context.Projects.CountAsync(project => project.IsActive)
                : isManager
                    ? managedProjectIds.Count
                    : await timesheetEntries.Select(entry => entry.ProjectId).Distinct().CountAsync(),
            HoursThisWeek = await timesheetEntries
                .Where(entry => entry.WorkDate >= startOfWeek && entry.WorkDate <= today)
                .SumAsync(entry => (decimal?)entry.Hours) ?? 0m,
            HoursThisMonth = await timesheetEntries
                .Where(entry => entry.WorkDate >= startOfMonth && entry.WorkDate <= today)
                .SumAsync(entry => (decimal?)entry.Hours) ?? 0m,
            PendingApprovals = isAdmin
                ? await context.TimesheetEntries.CountAsync(entry => entry.ApprovalStatus == TimesheetApprovalStatus.Pending)
                : isManager
                    ? await context.TimesheetEntries.CountAsync(entry =>
                        entry.ApprovalStatus == TimesheetApprovalStatus.Pending &&
                        entry.EmployeeId != currentEmployeeId &&
                        (managedEmployeeIds.Contains(entry.EmployeeId) ||
                         (entry.ProjectId.HasValue && managedProjectIds.Contains(entry.ProjectId.Value))))
                    : await context.TimesheetEntries.CountAsync(entry =>
                        entry.EmployeeId == currentEmployeeId &&
                        entry.ApprovalStatus == TimesheetApprovalStatus.Pending),
            RecentEntries = await timesheetEntries
                .Include(entry => entry.Employee)
                .Include(entry => entry.Project)
                .OrderByDescending(entry => entry.WorkDate)
                .ThenByDescending(entry => entry.Id)
                .Take(8)
                .Select(entry => new DashboardRecentEntryViewModel
                {
                    WorkDate = entry.WorkDate,
                    EmployeeName = entry.Employee!.FullName,
                    ProjectName = entry.Project != null ? entry.Project.Name : "-",
                    Hours = entry.Hours,
                    IsBillable = entry.IsBillable,
                    ApprovalStatus = entry.ApprovalStatus,
                    Description = entry.Description
                })
                .ToListAsync(),
            EmployeeHoursThisMonth = await timesheetEntries
                .Include(entry => entry.Employee)
                .Where(entry => entry.WorkDate >= startOfMonth && entry.WorkDate <= today)
                .GroupBy(entry => entry.Employee!.FullName)
                .Select(group => new EmployeeHoursSummaryViewModel
                {
                    EmployeeName = group.Key,
                    TotalHours = group.Sum(entry => entry.Hours),
                    BillableHours = group.Where(entry => entry.IsBillable).Sum(entry => entry.Hours)
                })
                .OrderByDescending(entry => entry.TotalHours)
                .ToListAsync()
        };

        logger.LogDebug("Dashboard loaded with {RecentEntries} recent entries.", model.RecentEntries.Count);

        return View(model);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private int GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var employeeId) ? employeeId : 0;
    }
}
