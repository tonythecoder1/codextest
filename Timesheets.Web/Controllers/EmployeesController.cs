using System.Security.Claims;
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

[Authorize(Roles = $"{AppRoles.Admin},{AppRoles.Manager}")]
public class EmployeesController(
    AppDbContext context,
    IPasswordResetService passwordResetService,
    INotificationService notificationService) : Controller
{
    public async Task<IActionResult> Index()
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        var query = context.Employees
            .AsNoTracking()
            .Include(employee => employee.Responsible)
            .AsQueryable();

        if (!IsAdmin())
        {
            query = query.Where(employee => employee.Id == currentEmployeeId || employee.ResponsibleId == currentEmployeeId);
        }

        var employees = await query
            .OrderByDescending(employee => employee.IsActive)
            .ThenBy(employee => employee.FullName)
            .ToListAsync();

        return View(employees);
    }

    public async Task<IActionResult> Create()
    {
        var employee = new Employee
        {
            IsActive = true,
            ResponsibleId = IsAdmin() ? null : GetCurrentEmployeeId()
        };

        await PopulateFormOptionsAsync(employee);
        return View(employee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee employee)
    {
        ModelState.Remove(nameof(employee.PasswordHash));

        if (!IsAdmin())
        {
            employee.IsAdmin = false;
            employee.IsManager = false;
            employee.ResponsibleId = GetCurrentEmployeeId();
        }

        if (await context.Employees.AnyAsync(item => item.Email == employee.Email))
        {
            ModelState.AddModelError(nameof(employee.Email), "Já existe um colaborador com este email.");
        }

        if (string.IsNullOrWhiteSpace(employee.Password))
        {
            ModelState.AddModelError(nameof(employee.Password), "A password é obrigatória.");
        }

        await ValidateEmployeeAsync(employee);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(employee);
            return View(employee);
        }

        employee.PasswordHash = PasswordHasher.Hash(employee.Password!);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = employee.IsManager
            ? "Responsável criado com sucesso."
            : "Colaborador criado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var employee = await FindScopedEmployeeAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

        await PopulateFormOptionsAsync(employee);
        return View(employee);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Employee employee)
    {
        ModelState.Remove(nameof(employee.PasswordHash));

        if (id != employee.Id)
        {
            return NotFound();
        }

        var existingEmployee = await FindScopedEmployeeAsync(id, track: true);
        if (existingEmployee is null)
        {
            return NotFound();
        }

        if (await context.Employees.AnyAsync(item => item.Email == employee.Email && item.Id != employee.Id))
        {
            ModelState.AddModelError(nameof(employee.Email), "Já existe um colaborador com este email.");
        }

        if (!IsAdmin())
        {
            employee.IsAdmin = existingEmployee.IsAdmin;
            employee.IsManager = existingEmployee.IsManager;
            employee.ResponsibleId = existingEmployee.Id == GetCurrentEmployeeId()
                ? existingEmployee.ResponsibleId
                : GetCurrentEmployeeId();
        }

        await ValidateEmployeeAsync(employee, existingEmployee.Id);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(employee);
            return View(employee);
        }

        existingEmployee.FullName = employee.FullName;
        existingEmployee.Email = employee.Email;
        existingEmployee.JobTitle = employee.JobTitle;
        existingEmployee.IsActive = employee.IsActive;
        existingEmployee.IsAdmin = employee.IsAdmin;
        existingEmployee.IsManager = employee.IsManager;
        existingEmployee.ResponsibleId = employee.ResponsibleId;

        var newPassword = employee.Password;
        var passwordChanged = !string.IsNullOrWhiteSpace(newPassword);
        if (passwordChanged)
        {
            existingEmployee.PasswordHash = PasswordHasher.Hash(newPassword!);
        }

        await context.SaveChangesAsync();

        if (passwordChanged)
        {
            await notificationService.NotifyAsync(
                existingEmployee.Id,
                AppNotificationType.PasswordChanged,
                "Password alterada",
                "A tua password do TimeFlow foi alterada no centro de gestão.",
                Url.Action("Login", "Account"),
                sendEmail: true,
                emailSubject: "TimeFlow · Password alterada");
        }

        TempData["StatusMessage"] = employee.IsManager
            ? "Responsável atualizado."
            : "Colaborador atualizado.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> PasswordCenter()
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        var query = context.Employees
            .AsNoTracking()
            .AsQueryable();

        if (!IsAdmin())
        {
            query = query.Where(employee => employee.ResponsibleId == currentEmployeeId);
        }

        var employees = await query
            .OrderByDescending(employee => employee.IsActive)
            .ThenBy(employee => employee.FullName)
            .Select(employee => new PasswordCenterEmployeeViewModel
            {
                Id = employee.Id,
                FullName = employee.FullName,
                Email = employee.Email,
                JobTitle = employee.JobTitle,
                IsActive = employee.IsActive
            })
            .ToListAsync();

        return View(new PasswordCenterViewModel { Employees = employees });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendPasswordReset(int id)
    {
        var employee = await FindPasswordScopedEmployeeAsync(id, track: true);

        if (employee is null || !employee.IsActive)
        {
            return NotFound();
        }

        var token = await passwordResetService.CreateTokenAsync(employee, GetCurrentEmployeeId());
        var resetUrl = Url.Action("ResetPassword", "Account", new { token }, Request.Scheme);

        await notificationService.NotifyAsync(
            employee.Id,
            AppNotificationType.PasswordReset,
            "Reposição de password",
            $"O administrador enviou um link para repores a tua password no TimeFlow.\n\nLink: {resetUrl}\n\nEste link expira em 2 horas.",
            resetUrl,
            sendEmail: true,
            emailSubject: "TimeFlow · Reposição de password");

        TempData["StatusMessage"] = $"Link de reposição enviado para {employee.Email}.";
        return RedirectToAction(nameof(PasswordCenter));
    }

    public async Task<IActionResult> ChangePassword(int id)
    {
        var employee = await FindPasswordScopedEmployeeAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

        return View(new AdminPasswordChangeViewModel
        {
            EmployeeId = employee.Id,
            FullName = employee.FullName,
            Email = employee.Email
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(int id, AdminPasswordChangeViewModel model)
    {
        if (id != model.EmployeeId)
        {
            return NotFound();
        }

        var employee = await FindPasswordScopedEmployeeAsync(id, track: true);
        if (employee is null)
        {
            return NotFound();
        }

        model.FullName = employee.FullName;
        model.Email = employee.Email;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        employee.PasswordHash = PasswordHasher.Hash(model.Password);
        await context.SaveChangesAsync();

        await notificationService.NotifyAsync(
            employee.Id,
            AppNotificationType.PasswordChanged,
            "Password alterada",
            "A tua password do TimeFlow foi alterada no centro de gestão.",
            Url.Action("Login", "Account"),
            sendEmail: true,
            emailSubject: "TimeFlow · Password alterada");

        TempData["StatusMessage"] = $"Password atualizada para {employee.FullName}.";
        return RedirectToAction(nameof(PasswordCenter));
    }

    private async Task<Employee?> FindPasswordScopedEmployeeAsync(int id, bool track = false)
    {
        IQueryable<Employee> query = track
            ? context.Employees
            : context.Employees.AsNoTracking();

        if (IsAdmin())
        {
            return await query.FirstOrDefaultAsync(employee => employee.Id == id);
        }

        var currentEmployeeId = GetCurrentEmployeeId();
        return await query.FirstOrDefaultAsync(employee =>
            employee.Id == id &&
            employee.ResponsibleId == currentEmployeeId);
    }

    private async Task<Employee?> FindScopedEmployeeAsync(int id, bool track = false)
    {
        IQueryable<Employee> query = track
            ? context.Employees.Include(employee => employee.Responsible)
            : context.Employees.AsNoTracking().Include(employee => employee.Responsible);

        if (IsAdmin())
        {
            return await query.FirstOrDefaultAsync(employee => employee.Id == id);
        }

        var currentEmployeeId = GetCurrentEmployeeId();
        return await query.FirstOrDefaultAsync(employee =>
            employee.Id == id &&
            (employee.Id == currentEmployeeId || employee.ResponsibleId == currentEmployeeId));
    }

    private async Task PopulateFormOptionsAsync(Employee employee)
    {
        var currentEmployeeId = GetCurrentEmployeeId();
        var selectedResponsibleId = employee.ResponsibleId;
        var responsibleQuery = context.Employees
            .AsNoTracking()
            .Where(item => (item.IsActive || item.Id == selectedResponsibleId) && (item.IsManager || item.IsAdmin))
            .OrderBy(item => item.FullName)
            .AsQueryable();

        if (!IsAdmin())
        {
            responsibleQuery = responsibleQuery.Where(item => item.Id == currentEmployeeId);
        }

        ViewBag.ResponsibleOptions = await responsibleQuery
            .Select(item => new SelectListItem
            {
                Value = item.Id.ToString(),
                Text = item.FullName,
                Selected = item.Id == selectedResponsibleId
            })
            .ToListAsync();

        ViewBag.CanSetPrivileges = IsAdmin();
        ViewBag.CurrentEmployeeId = currentEmployeeId;
    }

    private async Task ValidateEmployeeAsync(Employee employee, int? existingEmployeeId = null)
    {
        if (!employee.IsAdmin && !employee.IsManager && !employee.ResponsibleId.HasValue)
        {
            ModelState.AddModelError(nameof(employee.ResponsibleId), "Seleciona um responsável.");
        }

        if (employee.ResponsibleId.HasValue)
        {
            if (existingEmployeeId.HasValue && employee.ResponsibleId.Value == existingEmployeeId.Value)
            {
                ModelState.AddModelError(nameof(employee.ResponsibleId), "Um colaborador não pode ser o próprio responsável.");
            }
            else
            {
                var responsible = await context.Employees
                    .AsNoTracking()
                    .FirstOrDefaultAsync(item => item.Id == employee.ResponsibleId.Value);

                if (responsible is null || (!responsible.IsManager && !responsible.IsAdmin))
                {
                    ModelState.AddModelError(nameof(employee.ResponsibleId), "Seleciona um responsável válido.");
                }
            }
        }
    }

    private bool IsAdmin()
    {
        return User.IsInRole(AppRoles.Admin);
    }

    private int GetCurrentEmployeeId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(value, out var employeeId) ? employeeId : 0;
    }
}
