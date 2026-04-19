using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class TimesheetEntriesController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index(int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        if (!IsAdmin())
        {
            employeeId = GetCurrentEmployeeId();
        }

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
            CanChooseEmployee = IsAdmin(),
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
        var isAdmin = IsAdmin();
        var model = new TimesheetEntryCreateViewModel
        {
            CanChooseEmployee = isAdmin,
            EmployeeId = isAdmin ? 0 : GetCurrentEmployeeId(),
            Employees = isAdmin ? await GetEmployeeOptionsAsync() : [],
            Projects = await GetProjectCalendarOptionsAsync()
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TimesheetEntryCreateViewModel model)
    {
        var isAdmin = IsAdmin();
        model.CanChooseEmployee = isAdmin;

        if (!isAdmin)
        {
            model.EmployeeId = GetCurrentEmployeeId();
            ModelState.Remove(nameof(model.EmployeeId));
        }

        var entries = ParseEntries(model.EntriesJson);
        if (entries.Count == 0)
        {
            ModelState.AddModelError(nameof(model.EntriesJson), "Adiciona pelo menos um registo através do calendário.");
        }

        await ValidateCreateEntriesAsync(model.EmployeeId, entries);

        if (!await context.Employees.AnyAsync(e => e.Id == model.EmployeeId && e.IsActive))
        {
            ModelState.AddModelError(nameof(model.EmployeeId), "Seleciona um colaborador ativo.");
        }

        if (!ModelState.IsValid)
        {
            await PopulateCreateSelectsAsync(model);
            return View(model);
        }

        var timesheetEntries = entries.Select(entry => new TimesheetEntry
            {
                EmployeeId = model.EmployeeId,
                ProjectId = entry.ProjectId,
                WorkDate = entry.WorkDate.Date,
                Hours = entry.Hours,
                Description = entry.Description.Trim(),
                IsBillable = entry.IsBillable
            })
            .ToList();

        context.TimesheetEntries.AddRange(timesheetEntries);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = timesheetEntries.Count == 1
            ? "Registo criado com sucesso."
            : $"{timesheetEntries.Count} registos criados com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var entry = await context.TimesheetEntries.FindAsync(id);
        if (entry is null)
        {
            return NotFound();
        }

        if (!CanManageEntry(entry.EmployeeId))
        {
            return Forbid();
        }

        var isAdmin = IsAdmin();
        var model = new TimesheetEntryFormViewModel
        {
            Id = entry.Id,
            CanChooseEmployee = isAdmin,
            EmployeeId = entry.EmployeeId,
            ProjectId = entry.ProjectId,
            WorkDate = entry.WorkDate,
            Hours = entry.Hours,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Employees = isAdmin ? await GetEmployeeOptionsAsync(entry.EmployeeId) : [],
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

        var isAdmin = IsAdmin();
        model.CanChooseEmployee = isAdmin;

        if (!isAdmin)
        {
            model.EmployeeId = GetCurrentEmployeeId();
            ModelState.Remove(nameof(model.EmployeeId));
        }

        await ValidateSingleEntryAsync(model.EmployeeId, model.ProjectId, model.WorkDate, model.Hours, id);

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

        if (!CanManageEntry(entry.EmployeeId))
        {
            return Forbid();
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

        if (!CanManageEntry(entry.EmployeeId))
        {
            return Forbid();
        }

        context.TimesheetEntries.Remove(entry);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo removido.";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateSelectsAsync(TimesheetEntryFormViewModel model)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(model.EmployeeId) : [];
        model.Projects = await GetProjectOptionsAsync(model.ProjectId);
    }

    private async Task PopulateCreateSelectsAsync(TimesheetEntryCreateViewModel model)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(model.EmployeeId) : [];
        model.Projects = await GetProjectCalendarOptionsAsync();
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

    private async Task<List<ProjectCalendarOptionViewModel>> GetProjectCalendarOptionsAsync()
    {
        return await context.Projects
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Name)
            .Select(p => new ProjectCalendarOptionViewModel
            {
                Id = p.Id,
                Name = p.Name,
                ClientName = p.ClientName,
                ColorHex = p.ColorHex,
                AllowWeekendWork = p.AllowWeekendWork
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

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    private int GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var employeeId) ? employeeId : 0;
    }

    private bool CanManageEntry(int employeeId)
    {
        return IsAdmin() || employeeId == GetCurrentEmployeeId();
    }

    private List<TimesheetEntryCreateInput> ParseEntries(string entriesJson)
    {
        if (string.IsNullOrWhiteSpace(entriesJson))
        {
            return [];
        }

        try
        {
            var entries = JsonSerializer.Deserialize<List<TimesheetEntryCreateInput>>(
                entriesJson,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return entries ?? [];
        }
        catch (JsonException)
        {
            ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Os registos do calendário não são válidos.");
            return [];
        }
    }

    private async Task ValidateCreateEntriesAsync(int employeeId, List<TimesheetEntryCreateInput> entries)
    {
        var projects = await context.Projects
            .AsNoTracking()
            .Where(p => p.IsActive)
            .ToDictionaryAsync(p => p.Id);

        foreach (var entry in entries)
        {
            if (!projects.TryGetValue(entry.ProjectId, out var project))
            {
                ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Um dos registos usa um projeto inválido.");
                continue;
            }

            if (entry.Hours is < 0.25m or > 12m)
            {
                ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Cada registo deve ter entre 0,25h e 12h.");
            }

            if (string.IsNullOrWhiteSpace(entry.Description))
            {
                ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Cada registo precisa de descrição.");
            }

            if (IsWeekend(entry.WorkDate) && !project.AllowWeekendWork)
            {
                ModelState.AddModelError(
                    nameof(TimesheetEntryCreateViewModel.EntriesJson),
                    $"O projeto \"{project.Name}\" não permite registos ao fim de semana.");
            }
        }

        foreach (var dayGroup in entries.GroupBy(e => e.WorkDate.Date))
        {
            var existingHours = await context.TimesheetEntries
                .Where(e => e.EmployeeId == employeeId && e.WorkDate == dayGroup.Key)
                .SumAsync(e => (decimal?)e.Hours) ?? 0m;

            if (existingHours + dayGroup.Sum(e => e.Hours) > 12m)
            {
                ModelState.AddModelError(
                    nameof(TimesheetEntryCreateViewModel.EntriesJson),
                    $"O total de horas em {dayGroup.Key:dd/MM/yyyy} não pode ultrapassar 12h.");
            }
        }
    }

    private async Task ValidateSingleEntryAsync(int employeeId, int projectId, DateTime workDate, decimal hours, int? entryIdToIgnore)
    {
        var project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(p => p.Id == projectId && p.IsActive);
        if (project is null)
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.ProjectId), "Seleciona um projeto ativo.");
            return;
        }

        if (hours is < 0.25m or > 12m)
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.Hours), "As horas devem estar entre 0,25h e 12h.");
        }

        if (IsWeekend(workDate) && !project.AllowWeekendWork)
        {
            ModelState.AddModelError(
                nameof(TimesheetEntryFormViewModel.WorkDate),
                $"O projeto \"{project.Name}\" não permite registos ao fim de semana.");
        }

        var existingHoursQuery = context.TimesheetEntries
            .Where(e => e.EmployeeId == employeeId && e.WorkDate == workDate.Date);

        if (entryIdToIgnore.HasValue)
        {
            existingHoursQuery = existingHoursQuery.Where(e => e.Id != entryIdToIgnore.Value);
        }

        var existingHours = await existingHoursQuery.SumAsync(e => (decimal?)e.Hours) ?? 0m;
        if (existingHours + hours > 12m)
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.Hours), "O total de horas neste dia não pode ultrapassar 12h.");
        }
    }

    private static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
    }
}
