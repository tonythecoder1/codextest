using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

public class TimesheetEntriesController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index(int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        var query = context.TimesheetEntries
            .AsNoTracking()
            .Include(e => e.Employee)
            .Include(e => e.Project)
            .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(e => e.EmployeeId == employeeId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(e => e.ProjectId == projectId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(e => e.WorkDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(e => e.WorkDate <= toDate.Value.Date);
        }

        var orderedEntries = query
            .OrderByDescending(e => e.WorkDate)
            .ThenByDescending(e => e.Id);

        var model = new TimesheetEntriesIndexViewModel
        {
            EmployeeId = employeeId,
            ProjectId = projectId,
            FromDate = fromDate,
            ToDate = toDate,
            TotalHours = await query.SumAsync(e => (decimal?)e.Hours) ?? 0m,
            EntryCount = await query.CountAsync(),
            Entries = await orderedEntries
                .Select(e => new TimesheetEntryListItemViewModel
                {
                    Id = e.Id,
                    WorkDate = e.WorkDate,
                    EmployeeName = e.Employee!.FullName,
                    ProjectName = e.Project!.Name,
                    ProjectColorHex = e.Project!.ColorHex,
                    Hours = e.Hours,
                    IsBillable = e.IsBillable,
                    Description = e.Description
                })
                .ToListAsync(),
            Employees = await GetEmployeeOptionsAsync(employeeId),
            Projects = await GetProjectOptionsAsync(projectId)
        };

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        var model = new TimesheetEntryFormViewModel
        {
            WorkDate = DateTime.Today,
            Employees = await GetEmployeeOptionsAsync(),
            Projects = await GetProjectOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TimesheetEntryFormViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateSelectsAsync(model);
            return View(model);
        }

        var entry = new TimesheetEntry
        {
            EmployeeId = model.EmployeeId,
            ProjectId = model.ProjectId,
            WorkDate = model.WorkDate.Date,
            Hours = model.Hours,
            Description = model.Description,
            IsBillable = model.IsBillable
        };

        context.TimesheetEntries.Add(entry);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo criado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var entry = await context.TimesheetEntries.FindAsync(id);
        if (entry is null)
        {
            return NotFound();
        }

        var model = new TimesheetEntryFormViewModel
        {
            Id = entry.Id,
            EmployeeId = entry.EmployeeId,
            ProjectId = entry.ProjectId,
            WorkDate = entry.WorkDate,
            Hours = entry.Hours,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Employees = await GetEmployeeOptionsAsync(entry.EmployeeId),
            Projects = await GetProjectOptionsAsync(entry.ProjectId)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, TimesheetEntryFormViewModel model)
    {
        if (id != model.Id)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            await PopulateSelectsAsync(model);
            return View(model);
        }

        var entry = await context.TimesheetEntries.FindAsync(id);
        if (entry is null)
        {
            return NotFound();
        }

        entry.EmployeeId = model.EmployeeId;
        entry.ProjectId = model.ProjectId;
        entry.WorkDate = model.WorkDate.Date;
        entry.Hours = model.Hours;
        entry.Description = model.Description;
        entry.IsBillable = model.IsBillable;

        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo atualizado.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entry = await context.TimesheetEntries.FindAsync(id);
        if (entry is null)
        {
            return NotFound();
        }

        context.TimesheetEntries.Remove(entry);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo removido.";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectsAsync(TimesheetEntryFormViewModel model)
    {
        model.Employees = await GetEmployeeOptionsAsync(model.EmployeeId);
        model.Projects = await GetProjectOptionsAsync(model.ProjectId);
    }

    private async Task<List<SelectListItem>> GetEmployeeOptionsAsync(int? selectedId = null)
    {
        return await context.Employees
            .AsNoTracking()
            .Where(e => e.IsActive || e.Id == selectedId)
            .OrderBy(e => e.FullName)
            .Select(e => new SelectListItem
            {
                Value = e.Id.ToString(),
                Text = e.FullName,
                Selected = e.Id == selectedId
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetProjectOptionsAsync(int? selectedId = null)
    {
        return await context.Projects
            .AsNoTracking()
            .Where(p => p.IsActive || p.Id == selectedId)
            .OrderBy(p => p.Name)
            .Select(p => new SelectListItem
            {
                Value = p.Id.ToString(),
                Text = p.Name,
                Selected = p.Id == selectedId
            })
            .ToListAsync();
    }
}
