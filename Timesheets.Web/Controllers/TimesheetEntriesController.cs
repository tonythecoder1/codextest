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
using Timesheets.Web.Security;
using Timesheets.Web.Services;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

[Authorize]
public class TimesheetEntriesController(
    AppDbContext context,
    ITimesheetPdfService pdfService,
    INotificationService notificationService) : Controller
{
    public async Task<IActionResult> Index(int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate, int? pdfEmployeeId, string? selectedMonth)
    {
        var access = await GetAccessContextAsync();
        if (!access.IsAdmin && !access.IsManager)
        {
            employeeId = access.CurrentEmployeeId;
        }

        var query = ApplyEntryScope(
            context.TimesheetEntries
                .AsNoTracking()
                .Include(entry => entry.Employee)
                .Include(entry => entry.Project)
                .Include(entry => entry.ApprovedByEmployee),
            access);

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

        var employeeOptions = access.CanChooseEmployees
            ? await GetEmployeeOptionsAsync(access, employeeId)
            : [];

        var effectivePdfEmployeeId = access.IsAdmin
            ? pdfEmployeeId ?? await context.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive)
                .OrderBy(employee => employee.FullName)
                .Select(employee => (int?)employee.Id)
                .FirstOrDefaultAsync()
            : access.CurrentEmployeeId;

        var pdfEmployeeOptions = access.IsAdmin
            ? await GetEmployeeOptionsAsync(access, effectivePdfEmployeeId)
            : [];

        var completedMonths = effectivePdfEmployeeId.HasValue
            ? await GetCompletedMonthOptionsAsync(effectivePdfEmployeeId.Value)
            : [];

        var selectedMonthValue = !string.IsNullOrWhiteSpace(selectedMonth) && completedMonths.Any(item => item.Value == selectedMonth)
            ? selectedMonth
            : completedMonths.FirstOrDefault()?.Value ?? string.Empty;

        var entryRows = await query
            .OrderByDescending(entry => entry.WorkDate)
            .ThenByDescending(entry => entry.Id)
            .Select(entry => new
            {
                entry.Id,
                entry.WorkDate,
                entry.EmployeeId,
                EmployeeName = entry.Employee!.FullName,
                ProjectName = entry.Project != null ? entry.Project.Name : "-",
                ProjectColorHex = entry.Project != null ? entry.Project.ColorHex : "#6B8760",
                entry.ProjectId,
                entry.Hours,
                entry.IsBillable,
                entry.EntryType,
                entry.Description,
                entry.ApprovalStatus,
                ApprovedByEmployeeName = entry.ApprovedByEmployee != null ? entry.ApprovedByEmployee.FullName : null,
                entry.ApprovedAt
            })
            .ToListAsync();

        var pendingApprovalRows = new List<TimesheetEntryListItemViewModel>();
        if (access.CanApproveEntries)
        {
            pendingApprovalRows = await ApplyEntryScope(
                    context.TimesheetEntries
                        .AsNoTracking()
                        .Include(entry => entry.Employee)
                        .Include(entry => entry.Project),
                    access)
                .Where(entry => entry.ApprovalStatus == TimesheetApprovalStatus.Pending)
                .OrderBy(entry => entry.WorkDate)
                .ThenBy(entry => entry.Id)
                .Select(entry => new TimesheetEntryListItemViewModel
                {
                    Id = entry.Id,
                    EmployeeId = entry.EmployeeId,
                    WorkDate = entry.WorkDate,
                    EmployeeName = entry.Employee!.FullName,
                    ProjectName = entry.Project != null ? entry.Project.Name : "-",
                    ProjectColorHex = entry.Project != null ? entry.Project.ColorHex : "#6B8760",
                    ProjectId = entry.ProjectId,
                    Hours = entry.Hours,
                    IsBillable = entry.IsBillable,
                    EntryType = entry.EntryType,
                    Description = entry.Description,
                    ApprovalStatus = entry.ApprovalStatus
                })
                .ToListAsync();
        }

        var model = new TimesheetEntriesIndexViewModel
        {
            CanChooseEmployee = access.CanChooseEmployees,
            CanChoosePdfEmployee = access.IsAdmin,
            CanApproveEntries = access.CanApproveEntries,
            EmployeeId = employeeId,
            ProjectId = projectId,
            FromDate = fromDate,
            ToDate = toDate,
            PdfEmployeeId = effectivePdfEmployeeId,
            SelectedMonth = selectedMonthValue,
            CompletedMonths = completedMonths,
            CanGenerateMonthlyPdf = effectivePdfEmployeeId.HasValue && completedMonths.Count > 0,
            MonthlyPdfHelpText = BuildMonthlyPdfHelpText(effectivePdfEmployeeId, completedMonths.Count),
            TotalHours = entryRows.Sum(entry => entry.Hours),
            EntryCount = entryRows.Count,
            Employees = employeeOptions,
            PdfEmployees = pdfEmployeeOptions,
            Projects = await GetProjectOptionsAsync(projectId),
            PendingApprovalEntries = pendingApprovalRows
                .Where(entry => CanApproveEntry(access, entry.EmployeeId, entry.ProjectId, entry.ApprovalStatus))
                .Take(8)
                .Select(entry =>
                {
                    entry.CanApprove = true;
                    return entry;
                })
                .ToList(),
            Entries = entryRows.Select(entry => new TimesheetEntryListItemViewModel
            {
                Id = entry.Id,
                EmployeeId = entry.EmployeeId,
                ProjectId = entry.ProjectId,
                WorkDate = entry.WorkDate,
                EmployeeName = entry.EmployeeName,
                ProjectName = entry.ProjectName,
                ProjectColorHex = entry.ProjectColorHex,
                Hours = entry.Hours,
                IsBillable = entry.IsBillable,
                EntryType = entry.EntryType,
                ApprovalStatus = entry.ApprovalStatus,
                ApprovedByEmployeeName = entry.ApprovedByEmployeeName,
                ApprovedAt = entry.ApprovedAt,
                CanApprove = CanApproveEntry(access, entry.EmployeeId, entry.ProjectId, entry.ApprovalStatus),
                Description = entry.Description
            }).ToList()
        };

        if (access.IsAdmin)
        {
            model.PdfMonthLookup = await GetPdfMonthLookupAsync(pdfEmployeeOptions);
        }

        return View(model);
    }

    public async Task<IActionResult> Create()
    {
        var access = await GetAccessContextAsync();
        var model = new TimesheetEntryCreateViewModel
        {
            CanChooseEmployee = access.CanChooseEmployees,
            IsManagerContext = access.IsManager,
            EmployeeId = access.CanChooseEmployees ? access.CurrentEmployeeId : access.CurrentEmployeeId,
            Employees = access.CanChooseEmployees ? await GetEmployeeOptionsAsync(access, access.CurrentEmployeeId) : [],
            Projects = await GetProjectCalendarOptionsAsync(),
            EntryTypes = GetEntryTypeOptions(),
            ExistingEntries = await GetExistingCalendarEntriesAsync(access)
        };

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(TimesheetEntryCreateViewModel model)
    {
        var access = await GetAccessContextAsync();
        model.CanChooseEmployee = access.CanChooseEmployees;
        model.IsManagerContext = access.IsManager;

        if (!access.CanChooseEmployees)
        {
            model.EmployeeId = access.CurrentEmployeeId;
            ModelState.Remove(nameof(model.EmployeeId));
        }

        var entries = ParseEntries(model.EntriesJson);
        if (entries.Count == 0)
        {
            ModelState.AddModelError(nameof(model.EntriesJson), "Adiciona pelo menos um registo através do calendário.");
        }

        await ValidateCreateEntriesAsync(access, model.EmployeeId, entries);

        if (!ModelState.IsValid)
        {
            await PopulateCreateSelectsAsync(model, access);
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
                IsBillable = entry.EntryType == TimesheetEntryType.Work && entry.IsBillable,
                ApprovalStatus = TimesheetApprovalStatus.Pending,
                ApprovedByEmployeeId = null,
                ApprovedAt = null
            })
            .ToList();

        context.TimesheetEntries.AddRange(timesheetEntries);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = timesheetEntries.Count == 1
            ? "Registo criado e enviado para aprovação."
            : $"{timesheetEntries.Count} registos criados e enviados para aprovação.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var access = await GetAccessContextAsync();
        var entry = await context.TimesheetEntries
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == id);

        if (entry is null)
        {
            return NotFound();
        }

        if (!CanManageEntry(access, entry.EmployeeId, entry.ProjectId))
        {
            return Forbid();
        }

        var model = new TimesheetEntryFormViewModel
        {
            Id = entry.Id,
            CanChooseEmployee = access.CanChooseEmployees,
            CanApprove = CanApproveEntry(access, entry.EmployeeId, entry.ProjectId, entry.ApprovalStatus),
            ApprovalStatus = entry.ApprovalStatus,
            EmployeeId = entry.EmployeeId,
            EntryType = entry.EntryType,
            ProjectId = entry.ProjectId,
            WorkDate = entry.WorkDate,
            Hours = entry.Hours,
            Description = entry.Description,
            IsBillable = entry.IsBillable,
            Employees = access.CanChooseEmployees ? await GetEmployeeOptionsAsync(access, entry.EmployeeId) : [],
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

        var access = await GetAccessContextAsync();
        model.CanChooseEmployee = access.CanChooseEmployees;

        if (!access.CanChooseEmployees)
        {
            model.EmployeeId = access.CurrentEmployeeId;
            ModelState.Remove(nameof(model.EmployeeId));
        }

        var existingEntry = await context.TimesheetEntries
            .FirstOrDefaultAsync(item => item.Id == id);

        if (existingEntry is null)
        {
            return NotFound();
        }

        if (!CanManageEntry(access, existingEntry.EmployeeId, existingEntry.ProjectId))
        {
            return Forbid();
        }

        await ValidateSingleEntryAsync(access, model.EmployeeId, model.EntryType, model.ProjectId, model.WorkDate, model.Hours, id);

        if (!ModelState.IsValid)
        {
            model.ApprovalStatus = existingEntry.ApprovalStatus;
            model.CanApprove = CanApproveEntry(access, existingEntry.EmployeeId, existingEntry.ProjectId, existingEntry.ApprovalStatus);
            await PopulateEditSelectsAsync(model, access);
            return View(model);
        }

        existingEntry.EmployeeId = model.EmployeeId;
        existingEntry.EntryType = model.EntryType;
        existingEntry.ProjectId = model.EntryType == TimesheetEntryType.Work ? model.ProjectId : null;
        existingEntry.WorkDate = model.WorkDate.Date;
        existingEntry.Hours = model.Hours;
        existingEntry.Description = model.Description.Trim();
        existingEntry.IsBillable = model.EntryType == TimesheetEntryType.Work && model.IsBillable;
        existingEntry.ApprovalStatus = TimesheetApprovalStatus.Pending;
        existingEntry.ApprovedByEmployeeId = null;
        existingEntry.ApprovedAt = null;

        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo atualizado e reenviado para aprovação.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var access = await GetAccessContextAsync();
        var entry = await context.TimesheetEntries
            .Include(item => item.Employee)
            .Include(item => item.Project)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (entry is null)
        {
            return NotFound();
        }

        if (!CanManageEntry(access, entry.EmployeeId, entry.ProjectId))
        {
            return Forbid();
        }

        context.TimesheetEntries.Remove(entry);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Registo removido.";

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, int? employeeId, int? projectId, DateTime? fromDate, DateTime? toDate)
    {
        var access = await GetAccessContextAsync();
        var entry = await context.TimesheetEntries
            .FirstOrDefaultAsync(item => item.Id == id);

        if (entry is null)
        {
            return NotFound();
        }

        if (!CanApproveEntry(access, entry.EmployeeId, entry.ProjectId, entry.ApprovalStatus))
        {
            return Forbid();
        }

        entry.ApprovalStatus = TimesheetApprovalStatus.Approved;
        entry.ApprovedByEmployeeId = access.CurrentEmployeeId;
        entry.ApprovedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        await notificationService.NotifyAsync(
            entry.EmployeeId,
            AppNotificationType.TimesheetApproved,
            "Horas aprovadas",
            $"O teu registo de {entry.Hours:0.##}h em {entry.WorkDate:dd/MM/yyyy} foi aprovado.",
            Url.Action(nameof(Index), "TimesheetEntries"),
            sendEmail: true,
            emailSubject: "TimeFlow · Horas aprovadas");

        TempData["StatusMessage"] = "Registo aprovado.";

        return RedirectToAction(nameof(Index), new { employeeId, projectId, fromDate, toDate });
    }

    public async Task<IActionResult> MonthlyPdf(string month, int? employeeId)
    {
        if (!TryParseMonth(month, out var monthStart))
        {
            TempData["StatusMessage"] = "Seleciona um mês válido para gerar o PDF.";
            return RedirectToAction(nameof(Index), new { employeeId });
        }

        var access = await GetAccessContextAsync();
        var effectiveEmployeeId = access.IsAdmin
            ? employeeId
            : access.CurrentEmployeeId;

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
        var access = await GetAccessContextAsync();
        if (!access.IsAdmin)
        {
            employeeId = access.CurrentEmployeeId;
        }

        var query = ApplyEntryScope(
            context.TimesheetEntries
                .AsNoTracking()
                .Include(entry => entry.Employee)
                .Include(entry => entry.Project),
            access);

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

    private async Task PopulateEditSelectsAsync(TimesheetEntryFormViewModel model, AccessContext access)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(access, model.EmployeeId) : [];
        model.Projects = await GetProjectOptionsAsync(model.ProjectId);
        model.EntryTypes = GetEntryTypeOptions(model.EntryType);
    }

    private async Task PopulateCreateSelectsAsync(TimesheetEntryCreateViewModel model, AccessContext access)
    {
        model.Employees = model.CanChooseEmployee ? await GetEmployeeOptionsAsync(access, model.EmployeeId) : [];
        model.Projects = await GetProjectCalendarOptionsAsync();
        model.EntryTypes = GetEntryTypeOptions();
        model.ExistingEntries = await GetExistingCalendarEntriesAsync(access);
    }

    private async Task<List<SelectListItem>> GetEmployeeOptionsAsync(AccessContext access, int? selectedId = null)
    {
        var currentEmployeeId = access.CurrentEmployeeId;
        var employeeIds = access.IsAdmin
            ? await context.Employees
                .AsNoTracking()
                .Where(employee => employee.IsActive || employee.Id == selectedId)
                .OrderBy(employee => employee.FullName)
                .Select(employee => employee.Id)
                .ToListAsync()
            : new[] { currentEmployeeId }
                .Concat(access.ManagedEmployeeIds)
                .Distinct()
                .ToList();

        return await context.Employees
            .AsNoTracking()
            .Where(employee => employeeIds.Contains(employee.Id))
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

    private async Task<List<ExistingCalendarEntryViewModel>> GetExistingCalendarEntriesAsync(AccessContext access)
    {
        var query = ApplyEntryScope(
            context.TimesheetEntries
                .AsNoTracking()
                .Include(entry => entry.Project)
                .Include(entry => entry.ApprovedByEmployee),
            access);

        return await query
            .OrderBy(entry => entry.WorkDate)
            .ThenBy(entry => entry.Id)
            .Select(entry => new ExistingCalendarEntryViewModel
            {
                Id = entry.Id,
                EmployeeId = entry.EmployeeId,
                WorkDate = entry.WorkDate,
                EntryType = entry.EntryType,
                ApprovalStatus = entry.ApprovalStatus,
                ProjectId = entry.ProjectId,
                ProjectName = entry.Project != null ? entry.Project.Name : string.Empty,
                ProjectColorHex = entry.Project != null ? entry.Project.ColorHex : "#6B8760",
                Hours = entry.Hours,
                Description = entry.Description,
                ApprovedByEmployeeName = entry.ApprovedByEmployee != null ? entry.ApprovedByEmployee.FullName : null,
                ApprovedAt = entry.ApprovedAt
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

    private async Task<List<EmployeeCompletedMonthsViewModel>> GetPdfMonthLookupAsync(List<SelectListItem> employees)
    {
        var lookup = new List<EmployeeCompletedMonthsViewModel>();

        foreach (var employee in employees)
        {
            if (!int.TryParse(employee.Value, out var employeeId))
            {
                continue;
            }

            var months = await GetCompletedMonthOptionsAsync(employeeId);
            lookup.Add(new EmployeeCompletedMonthsViewModel
            {
                EmployeeId = employeeId,
                Months = months.Select(month => new CompletedMonthOptionViewModel
                {
                    Value = month.Value ?? string.Empty,
                    Text = month.Text ?? string.Empty
                }).ToList()
            });
        }

        return lookup;
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

    private static bool IsWeekend(DateTime date)
    {
        return date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
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

    private async Task ValidateCreateEntriesAsync(AccessContext access, int employeeId, List<TimesheetEntryCreateInput> entries)
    {
        if (!await context.Employees.AnyAsync(employee => employee.Id == employeeId && employee.IsActive))
        {
            ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EmployeeId), "Seleciona um colaborador ativo.");
        }

        if (!CanAssignEmployee(access, employeeId))
        {
            ModelState.AddModelError(nameof(TimesheetEntryCreateViewModel.EmployeeId), "Não tens permissões para lançar horas para este colaborador.");
        }

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

    private async Task ValidateSingleEntryAsync(AccessContext access, int employeeId, TimesheetEntryType entryType, int? projectId, DateTime workDate, decimal hours, int? entryIdToIgnore)
    {
        if (!await context.Employees.AnyAsync(employee => employee.Id == employeeId && employee.IsActive))
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.EmployeeId), "Seleciona um colaborador ativo.");
        }

        if (!CanAssignEmployee(access, employeeId))
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.EmployeeId), "Não tens permissões para alterar este colaborador.");
        }

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
                    ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.ProjectId), "Seleciona um projeto válido.");
                }
                else if (IsWeekend(workDate) && !project.AllowWeekendWork)
                {
                    ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.WorkDate), "O projeto selecionado não permite registos ao fim de semana.");
                }
            }
        }

        var existingHours = await context.TimesheetEntries
            .Where(entry => entry.EmployeeId == employeeId &&
                            entry.WorkDate == workDate.Date &&
                            (!entryIdToIgnore.HasValue || entry.Id != entryIdToIgnore.Value))
            .SumAsync(entry => (decimal?)entry.Hours) ?? 0m;

        if (existingHours + hours > 12m)
        {
            ModelState.AddModelError(nameof(TimesheetEntryFormViewModel.Hours), "O total diário não pode ultrapassar 12h.");
        }
    }

    private async Task<AccessContext> GetAccessContextAsync()
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        var isAdmin = User.IsInRole(AppRoles.Admin);
        var isManager = !isAdmin && User.IsInRole(AppRoles.Manager);

        if (!isManager)
        {
            return new AccessContext(isAdmin, false, currentEmployeeId, [], []);
        }

        var managedEmployeeIds = await context.Employees
            .AsNoTracking()
            .Where(employee => employee.ResponsibleId == currentEmployeeId)
            .Select(employee => employee.Id)
            .ToListAsync();

        var managedProjectIds = await context.Projects
            .AsNoTracking()
            .Where(project => project.ResponsibleEmployees.Any(employee => employee.Id == currentEmployeeId))
            .Select(project => project.Id)
            .ToListAsync();

        return new AccessContext(isAdmin, true, currentEmployeeId, managedEmployeeIds, managedProjectIds);
    }

    private IQueryable<TimesheetEntry> ApplyEntryScope(IQueryable<TimesheetEntry> query, AccessContext access)
    {
        if (access.IsAdmin)
        {
            return query;
        }

        if (access.IsManager)
        {
            return query.Where(entry =>
                entry.EmployeeId == access.CurrentEmployeeId ||
                access.ManagedEmployeeIds.Contains(entry.EmployeeId) ||
                (entry.ProjectId.HasValue && access.ManagedProjectIds.Contains(entry.ProjectId.Value)));
        }

        return query.Where(entry => entry.EmployeeId == access.CurrentEmployeeId);
    }

    private bool CanAssignEmployee(AccessContext access, int employeeId)
    {
        return access.IsAdmin ||
               employeeId == access.CurrentEmployeeId ||
               access.ManagedEmployeeIds.Contains(employeeId);
    }

    private bool CanManageEntry(AccessContext access, int employeeId, int? projectId)
    {
        if (access.IsAdmin)
        {
            return true;
        }

        if (employeeId == access.CurrentEmployeeId)
        {
            return true;
        }

        if (!access.IsManager)
        {
            return false;
        }

        return access.ManagedEmployeeIds.Contains(employeeId) ||
               (projectId.HasValue && access.ManagedProjectIds.Contains(projectId.Value));
    }

    private bool CanApproveEntry(AccessContext access, int employeeId, int? projectId, TimesheetApprovalStatus approvalStatus)
    {
        if (approvalStatus == TimesheetApprovalStatus.Approved)
        {
            return false;
        }

        if (access.IsAdmin)
        {
            return true;
        }

        if (!access.IsManager || employeeId == access.CurrentEmployeeId)
        {
            return false;
        }

        return access.ManagedEmployeeIds.Contains(employeeId) ||
               (projectId.HasValue && access.ManagedProjectIds.Contains(projectId.Value));
    }

    private int GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var employeeId) ? employeeId : 0;
    }

    private sealed record AccessContext(
        bool IsAdmin,
        bool IsManager,
        int CurrentEmployeeId,
        List<int> ManagedEmployeeIds,
        List<int> ManagedProjectIds)
    {
        public bool CanChooseEmployees => IsAdmin || IsManager;
        public bool CanApproveEntries => IsAdmin || IsManager;
    }
}
