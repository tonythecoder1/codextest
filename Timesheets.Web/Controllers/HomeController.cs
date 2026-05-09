using System.Diagnostics;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class HomeController(ILogger<HomeController> logger, AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var isAdmin = User.IsInRole("Admin");
        var currentEmployeeId = GetCurrentEmployeeId();
        var today = DateTime.Today;
        var daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var startOfWeek = today.AddDays(-daysSinceMonday);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);
        var timesheetEntries = context.TimesheetEntries.AsNoTracking().AsQueryable();

        if (!isAdmin)
        {
            timesheetEntries = timesheetEntries.Where(entry => entry.EmployeeId == currentEmployeeId);
        }

        var model = new DashboardViewModel
        {
            IsAdmin = isAdmin,
            PrimaryStatLabel = isAdmin ? "Colaboradores ativos" : "A minha equipa",
            SecondaryStatLabel = isAdmin ? "Projetos ativos" : "Os meus projetos",
            SummaryEyebrow = isAdmin ? "Capacidade" : "Resumo",
            SummarySectionTitle = isAdmin ? "Horas por colaborador" : "As minhas horas no mês",
            ActiveEmployees = isAdmin
                ? await context.Employees.CountAsync(e => e.IsActive)
                : 1,
            ActiveProjects = isAdmin
                ? await context.Projects.CountAsync(p => p.IsActive)
                : await timesheetEntries.Select(entry => entry.ProjectId).Distinct().CountAsync(),
            HoursThisWeek = await timesheetEntries
                .Where(e => e.WorkDate >= startOfWeek && e.WorkDate <= today)
                .SumAsync(e => (decimal?)e.Hours) ?? 0m,
            HoursThisMonth = await timesheetEntries
                .Where(e => e.WorkDate >= startOfMonth && e.WorkDate <= today)
                .SumAsync(e => (decimal?)e.Hours) ?? 0m,
            RecentEntries = await timesheetEntries
                .AsNoTracking()
                .Include(e => e.Employee)
                .Include(e => e.Project)
                .OrderByDescending(e => e.WorkDate)
                .ThenByDescending(e => e.Id)
                .Take(8)
                .Select(e => new DashboardRecentEntryViewModel
                {
                    WorkDate = e.WorkDate,
                    EmployeeName = e.Employee!.FullName,
                    ProjectName = e.Project!.Name,
                    Hours = e.Hours,
                    IsBillable = e.IsBillable,
                    Description = e.Description
                })
                .ToListAsync(),
            EmployeeHoursThisMonth = isAdmin
                ? await timesheetEntries
                    .AsNoTracking()
                    .Include(e => e.Employee)
                    .Where(e => e.WorkDate >= startOfMonth && e.WorkDate <= today)
                    .GroupBy(e => e.Employee!.FullName)
                    .Select(group => new EmployeeHoursSummaryViewModel
                    {
                        EmployeeName = group.Key,
                        TotalHours = group.Sum(e => e.Hours),
                        BillableHours = group.Where(e => e.IsBillable).Sum(e => e.Hours)
                    })
                    .OrderByDescending(e => e.TotalHours)
                    .ToListAsync()
                : await timesheetEntries
                .AsNoTracking()
                .Include(e => e.Employee)
                .Where(e => e.WorkDate >= startOfMonth && e.WorkDate <= today)
                .GroupBy(e => e.Employee!.FullName)
                .Select(group => new EmployeeHoursSummaryViewModel
                {
                    EmployeeName = group.Key,
                    TotalHours = group.Sum(e => e.Hours),
                    BillableHours = group.Where(e => e.IsBillable).Sum(e => e.Hours)
                })
                .OrderByDescending(e => e.TotalHours)
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
