using System.Diagnostics;
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
        var today = DateTime.Today;
        var daysSinceMonday = today.DayOfWeek == DayOfWeek.Sunday ? 6 : (int)today.DayOfWeek - 1;
        var startOfWeek = today.AddDays(-daysSinceMonday);
        var startOfMonth = new DateTime(today.Year, today.Month, 1);

        var model = new DashboardViewModel
        {
            ActiveEmployees = await context.Employees.CountAsync(e => e.IsActive),
            ActiveProjects = await context.Projects.CountAsync(p => p.IsActive),
            HoursThisWeek = await context.TimesheetEntries
                .Where(e => e.WorkDate >= startOfWeek && e.WorkDate <= today)
                .SumAsync(e => (decimal?)e.Hours) ?? 0m,
            HoursThisMonth = await context.TimesheetEntries
                .Where(e => e.WorkDate >= startOfMonth && e.WorkDate <= today)
                .SumAsync(e => (decimal?)e.Hours) ?? 0m,
            RecentEntries = await context.TimesheetEntries
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
            EmployeeHoursThisMonth = await context.TimesheetEntries
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
}
