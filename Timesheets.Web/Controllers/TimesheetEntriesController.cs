using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Services;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class TimesheetEntriesController(AppDbContext context, ITimesheetPdfService pdfService) : Controller
{
    public async Task<IActionResult> Index(int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate, string? selectedMonth)
    {
        if (!IsAdmin())
        {
            employeeId = GetCurrentEmployeeId();
        }

        var query = context.TimesheetEntries
            .AsNoTracking()
            .Include(entry => entry.Employee)
            .Include(entry => entry.Project)
            .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(entry => entry.EmployeeId == employeeId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(entry => entry.ProjectId == projectId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(entry => entry.WorkDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(entry => entry.WorkDate <= toDate.Value.Date);
        }

        var effectiveEmployeeId = employeeId ?? (IsAdmin() ? null : GetCurrentEmployeeId());
        var completedMonths = effectiveEmployeeId.HasValue
            ? await GetCompletedMonthOptionsAsync(effectiveEmployeeId.Value)
            : [];

        var selectedMonthValue = !string.IsNullOrWhiteSpace(selectedMonth) && completedMonths.Any(item => item.Value == selectedMonth)
            ? selectedMonth
            : completedMonths.FirstOrDefault()?.Value ?? string.Empty;

        var model = new TimesheetEntriesIndexViewModel
        {
            CanChooseEmployee = IsAdmin(),
            EmployeeId = employeeId,
            ProjectId = projectId,
            FromDate = fromDate,
            ToDate = toDate,
            SelectedMonth = selectedMonthValue,
            CompletedMonths = completedMonths,
            CanGenerateMonthlyPdf = completedMonths.Count > 0,
            MonthlyPdfHelpText = BuildMonthlyPdfHelpText(effectiveEmployeeId, completedMonths.Count),
            TotalHours = await query.SumAsync(entry => (decimal?)entry.Hours) ?? 0m,
            EntryCount = await query.CountAsync(),
            Entries = await query
                .OrderByDescending(entry => entry.WorkDate)
                .ThenByDescending(entry => entry.Id)
                .Select(entry => new TimesheetEntryListItemViewModel
                {
                    Id = entry.Id,
                    WorkDate = entry.WorkDate,
                    EmployeeName = entry.Employee!.FullName,
                    ProjectName = entry.Project != null ? entry.Project.Name : "-",
                    ProjectColorHex = entry.Project != null ? entry.Project.ColorHex : "#94A3B8",
                    Hours = entry.Hours,
                    IsBillable = entry.IsBillable,
                    EntryType = entry.EntryType,
                    Description = entry.Description
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
            Projects = await GetProjectCalendarOptionsAsync(),
            EntryTypes = GetEntryTypeOptions(),
            ExistingEntries = await GetExistingCalendarEntriesAsync()
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

        if (!await context.Employees.AnyAsync(employee => employee.Id == model.EmployeeId && employee.IsActive))
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
                EntryType = entry.EntryType,
                ProjectId = entry.EntryType == TimesheetEntryType.Work ? entry.ProjectId : null,
                WorkDate = entry.WorkDate.Date,
                Hours = entry.Hours,
                Description = entry.Description.Trim(),
                IsBillable = entry.EntryType == TimesheetEntryType.Work && entry.IsBillable
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
            EntryType = entry.EntryType,
            ProjectId = entry.ProjectId,
            WorkDate = entry.WorkDate,
            Hours = entry.Hours,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Employees = isAdmin ? await GetEmployeeOptionsAsync(entry.EmployeeId) : [],
            Projects = await GetProjectOptionsAsync(entry.ProjectId),
            EntryTypes = GetEntryTypeOptions(entry.EntryType)
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

        await ValidateSingleEntryAsync(model.EmployeeId, model.EntryType, model.ProjectId, model.WorkDate, model.Hours, id);

        if (!ModelState.IsValid)
        {
            await PopulateEditSelectsAsync(model);
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
        entry.EntryType = model.EntryType;
        entry.ProjectId = model.EntryType == TimesheetEntryType.Work ? model.ProjectId : null;
        entry.WorkDate = model.WorkDate.Date;
        entry.Hours = model.Hours;
        entry.Description = model.Description.Trim();
        entry.IsBillable = model.EntryType == TimesheetEntryType.Work && model.IsBillable;

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

    public async Task<IActionResult> MonthlyPdf(string month, int? employeeId)
    {
        if (!TryParseMonth(month, out var monthStart))
        {
            TempData["StatusMessage"] = "Seleciona um mês válido para gerar o PDF.";
            return RedirectToAction(nameof(Index), new { employeeId });
        }

        var effectiveEmployeeId = IsAdmin()
            ? employeeId
            : GetCurrentEmployeeId();

        if (!effectiveEmployeeId.HasValue)
        {
            TempData["StatusMessage"] = "Seleciona um colaborador para gerar o PDF mensal.";
            return RedirectToAction(nameof(Index));
        }

        var completedMonths = await GetCompletedMonthOptionsAsync(effectiveEmployeeId.Value);
        if (!completedMonths.Any(item => item.Value == month))
        {
            TempData["StatusMessage"] = "O PDF mensal só pode ser gerado quando o mês estiver completo.";
            return RedirectToAction(nameof(Index), new { employeeId = effectiveEmployeeId, selectedMonth = month });
        }

        var employee = await context.Employees.AsNoTracking().FirstAsync(item => item.Id == effectiveEmployeeId.Value);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);
        var entries = await context.TimesheetEntries
            .AsNoTracking()
            .Include(entry => entry.Project)
            .Where(entry => entry.EmployeeId == effectiveEmployeeId.Value &&
                            entry.WorkDate >= monthStart &&
                            entry.WorkDate <= monthEnd)
            .OrderBy(entry => entry.WorkDate)
            .ToListAsync();

        var pdf = pdfService.GenerateMonthlyPdf(new MonthlyTimesheetPdfModel
        {
            EmployeeName = employee.FullName,
            EmployeeEmail = employee.Email,
            MonthLabel = monthStart.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("pt-PT")),
            TotalHours = entries.Sum(entry => entry.Hours),
            Entries = entries.Select(entry => new MonthlyTimesheetPdfEntry
            {
                WorkDate = entry.WorkDate,
                TypeLabel = entry.EntryType.ToDisplayName(),
                ProjectName = entry.Project?.Name ?? string.Empty,
                Hours = entry.Hours,
                Description = entry.Description
            }).ToList()
        });

        var fileName = $"savana-timesheet-{employee.FullName.Replace(' ', '-').ToLowerInvariant()}-{month}.pdf";
        return File(pdf, "application/pdf", fileName);
    }

    public async Task<IActionResult> ReportPdf(int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        if (!IsAdmin())
        {
            employeeId = GetCurrentEmployeeId();
        }

        var query = context.TimesheetEntries
            .AsNoTracking()
            .Include(entry => entry.Employee)
            .Include(entry => entry.Project)
            .AsQueryable();

        if (employeeId.HasValue)
        {
            query = query.Where(entry => entry.EmployeeId == employeeId.Value);
        }

        if (projectId.HasValue)
        {
            query = query.Where(entry => entry.ProjectId == projectId.Value);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(entry => entry.WorkDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(entry => entry.WorkDate <= toDate.Value.Date);
        }

        var entries = await query
            .OrderByDescending(entry => entry.WorkDate)
            .ThenByDescending(entry => entry.Id)
            .ToListAsync();

        var scopeParts = new List<string>();
        if (employeeId.HasValue)
        {
            var employeeName = entries.FirstOrDefault()?.Employee?.FullName
                               ?? await context.Employees.Where(item => item.Id == employeeId.Value).Select(item => item.FullName).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(employeeName))
            {
                scopeParts.Add($"Colaborador: {employeeName}");
            }
        }

        if (projectId.HasValue)
        {
            var projectName = entries.FirstOrDefault(entry => entry.ProjectId == projectId.Value)?.Project?.Name
                              ?? await context.Projects.Where(item => item.Id == projectId.Value).Select(item => item.Name).FirstOrDefaultAsync();
            if (!string.IsNullOrWhiteSpace(projectName))
            {
                scopeParts.Add($"Projeto: {projectName}");
            }
        }

        if (fromDate.HasValue || toDate.HasValue)
        {
            scopeParts.Add($"Período: {(fromDate?.ToString("dd/MM/yyyy") ?? "início")} - {(toDate?.ToString("dd/MM/yyyy") ?? "fim")}");
        }

        var pdf = pdfService.GenerateReportPdf(new TimesheetReportPdfModel
        {
            ScopeLabel = string.Join(" · ", scopeParts),
            TotalEntries = entries.Count,
            TotalHours = entries.Sum(entry => entry.Hours),
            Entries = entries.Select(entry => new TimesheetReportPdfEntry
            {
                WorkDate = entry.WorkDate,
                EmployeeName = entry.Employee?.FullName ?? "-",
                TypeLabel = entry.EntryType.ToDisplayName(),
                ProjectName = entry.Project?.Name ?? string.Empty,
                Hours = entry.Hours,
                Description = entry.Description,
                IsBillable = entry.IsBillable
            }).ToList()
        });

        return File(pdf, "application/pdf", "savana-relatorio-horas.pdf");
    }

    private async Task PopulateEditSelectsAsync(TimesheetEntryFormViewModel model)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(model.EmployeeId) : [];
        model.Projects = await GetProjectOptionsAsync(model.ProjectId);
        model.EntryTypes = GetEntryTypeOptions(model.EntryType);
    }

    private async Task PopulateCreateSelectsAsync(TimesheetEntryCreateViewModel model)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(model.EmployeeId) : [];
        model.Projects = await GetProjectCalendarOptionsAsync();
        model.EntryTypes = GetEntryTypeOptions();
        model.ExistingEntries = await GetExistingCalendarEntriesAsync();
    }

    private async Task<List<SelectListItem>> GetEmployeeOptionsAsync(int? selectedId = null)
    {
        return await context.Employees
            .AsNoTracking()
            .Where(employee => employee.IsActive || employee.Id == selectedId)
            .OrderBy(employee => employee.FullName)
            .Select(employee => new SelectListItem
            {
                Value = employee.Id.ToString(),
                Text = employee.FullName,
                Selected = employee.Id == selectedId
            })
            .ToListAsync();
    }

    private List<SelectListItem> GetEntryTypeOptions(TimesheetEntryType? selectedType = null)
    {
        return Enum.GetValues<TimesheetEntryType>()
            .Select(entryType => new SelectListItem
            {
                Value = entryType.ToString(),
                Text = entryType.ToDisplayName(),
                Selected = entryType == selectedType
            })
            .ToList();
    }

    private async Task<List<ProjectCalendarOptionViewModel>> GetProjectCalendarOptionsAsync()
    {
        return await context.Projects
            .AsNoTracking()
            .Where(project => project.IsActive)
            .OrderBy(project => project.Name)
            .Select(project => new ProjectCalendarOptionViewModel
            {
                Id = project.Id,
                Name = project.Name,
                ClientName = project.ClientName,
                ColorHex = project.ColorHex,
                AllowWeekendWork = project.AllowWeekendWork
            })
            .ToListAsync();
    }

    private async Task<List<ExistingCalendarEntryViewModel>> GetExistingCalendarEntriesAsync()
    {
        var query = context.TimesheetEntries.AsNoTracking();

        if (!IsAdmin())
        {
            var employeeId = GetCurrentEmployeeId();
            query = query.Where(entry => entry.EmployeeId == employeeId);
        }

        return await query
            .OrderBy(entry => entry.WorkDate)
            .ThenBy(entry => entry.Id)
            .Select(entry => new ExistingCalendarEntryViewModel
            {
                EmployeeId = entry.EmployeeId,
                WorkDate = entry.WorkDate,
                EntryType = entry.EntryType,
                ProjectId = entry.ProjectId,
                Hours = entry.Hours,
                Description = entry.Description
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetProjectOptionsAsync(int? selectedId = null)
    {
        return await context.Projects
            .AsNoTracking()
            .Where(project => project.IsActive || project.Id == selectedId)
            .OrderBy(project => project.Name)
            .Select(project => new SelectListItem
            {
                Value = project.Id.ToString(),
                Text = project.Name,
                Selected = project.Id == selectedId
            })
            .ToListAsync();
    }

    private async Task<List<SelectListItem>> GetCompletedMonthOptionsAsync(int employeeId)
    {
        var dates = await context.TimesheetEntries
            .AsNoTracking()
            .Where(entry => entry.EmployeeId == employeeId)
            .Select(entry => entry.WorkDate)
            .ToListAsync();

        if (dates.Count == 0)
        {
            return [];
        }

        var coveredDays = dates
            .Select(day => day.Date)
            .Distinct()
            .ToHashSet();

        var firstMonth = new DateTime(dates.Min().Year, dates.Min().Month, 1);
        var lastMonth = new DateTime(dates.Max().Year, dates.Max().Month, 1);
        var completedMonths = new List<SelectListItem>();

        for (var monthCursor = firstMonth; monthCursor <= lastMonth; monthCursor = monthCursor.AddMonths(1))
        {
            if (IsMonthComplete(monthCursor, coveredDays))
            {
                completedMonths.Add(new SelectListItem
                {
                    Value = monthCursor.ToString("yyyy-MM"),
                    Text = monthCursor.ToString("MMMM yyyy", CultureInfo.GetCultureInfo("pt-PT"))
                });
            }
        }

        completedMonths.Reverse();
        return completedMonths;
    }

    private static bool IsMonthComplete(DateTime monthStart, HashSet<DateTime> coveredDays)
    {
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        for (var day = monthStart.Date; day <= monthEnd.Date; day = day.AddDays(1))
        {
            if (IsWeekend(day))
            {
                continue;
            }

            if (!coveredDays.Contains(day))
            {
                return false;
            }
        }

        return true;
    }

    private string BuildMonthlyPdfHelpText(int? employeeId, int completedMonthCount)
    {
        if (!employeeId.HasValue)
        {
            return "Seleciona um colaborador para gerar o PDF mensal.";
        }

        if (completedMonthCount == 0)
        {
            return "Ainda não existem meses completos para este utilizador.";
        }

        return "Só aparecem meses totalmente preenchidos.";
    }

    private bool TryParseMonth(string month, out DateTime monthStart)
    {
        return DateTime.TryParseExact(
            month,
            "yyyy-MM",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out monthStart);
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
                new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    Converters = { new JsonStringEnumConverter() }
                });

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
            .Where(project => project.IsActive)
            .ToDictionaryAsync(project => project.Id);

        foreach (var entry in entries)
        {
            if (entry.Hours is < 0.25m or > 12m)
            {
                ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Cada registo deve ter entre 0,25h e 12h.");
            }

            if (string.IsNullOrWhiteSpace(entry.Description))
            {
                ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Cada registo precisa de descrição.");
            }

            if (entry.EntryType == TimesheetEntryType.Work)
            {
                if (!entry.ProjectId.HasValue || !projects.TryGetValue(entry.ProjectId.Value, out var project))
                {
                    ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EntriesJson), "Um dos registos usa um projeto inválido.");
                    continue;
                }

                if (IsWeekend(entry.WorkDate) && !project.AllowWeekendWork)
                {
                    ModelState.AddModelError(
                        nameof(TimesheetEntryCreateViewModel.EntriesJson),
                        $"O projeto \"{project.Name}\" não permite registos ao fim de semana.");
                }
            }
        }

        foreach (var dayGroup in entries.GroupBy(entry => entry.WorkDate.Date))
        {
            var existingHours = await context.TimesheetEntries
                .Where(entry => entry.EmployeeId == employeeId && entry.WorkDate == dayGroup.Key)
                .SumAsync(entry => (decimal?)entry.Hours) ?? 0m;

            if (existingHours + dayGroup.Sum(entry => entry.Hours) > 12m)
            {
                ModelState.AddModelError(
                    nameof(TimesheetEntryCreateViewModel.EntriesJson),
                    $"O total de horas em {dayGroup.Key:dd/MM/yyyy} não pode ultrapassar 12h.");
            }
        }
    }

    private async Task ValidateSingleEntryAsync(int employeeId, TimesheetEntryType entryType, int? projectId, DateTime workDate, decimal hours, int? entryIdToIgnore)
    {
        if (hours is < 0.25m or > 12m)
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.Hours), "As horas devem estar entre 0,25h e 12h.");
        }

        Project? project = null;
        if (entryType == TimesheetEntryType.Work)
        {
            if (!projectId.HasValue)
            {
                ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.ProjectId), "Seleciona um projeto.");
            }
            else
            {
                project = await context.Projects.AsNoTracking().FirstOrDefaultAsync(item => item.Id == projectId.Value && item.IsActive);
                if (project is null)
                {
                    ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.ProjectId), "Seleciona um projeto ativo.");
                }
                else if (IsWeekend(workDate) && !project.AllowWeekendWork)
                {
                    ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.WorkDate), $"O projeto \"{project.Name}\" não permite registos ao fim de semana.");
                }
            }
        }

        var existingHoursQuery = context.TimesheetEntries
            .Where(entry => entry.EmployeeId == employeeId && entry.WorkDate == workDate.Date);

        if (entryIdToIgnore.HasValue)
        {
            existingHoursQuery = existingHoursQuery.Where(entry => entry.Id != entryIdToIgnore.Value);
        }

        var existingHours = await existingHoursQuery.SumAsync(entry => (decimal?)entry.Hours) ?? 0m;
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
