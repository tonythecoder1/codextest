using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Security;

namespace Timesheets.Web.Controllers;

[Authorize(Roles = "Admin")]
public class EmployeesController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var employees = await context.Employees
            .OrderByDescending(e => e.IsActive)
            .ThenBy(e => e.FullName)
            .ToListAsync();

        return View(employees);
    }

    public IActionResult Create()
    {
        return View(new Employee());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Employee employee)
    {
        ModelState.Remove(nameof(employee.PasswordHash));

        if (await context.Employees.AnyAsync(e => e.Email == employee.Email))
        {
            ModelState.AddModelError(nameof(employee.Email), "Já existe um colaborador com este email.");
        }

        if (string.IsNullOrWhiteSpace(employee.Password))
        {
            ModelState.AddModelError(nameof(employee.Password), "A password é obrigatória.");
        }

        if (!ModelState.IsValid)
        {
            return View(employee);
        }

        employee.PasswordHash = PasswordHasher.Hash(employee.Password!);
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Colaborador criado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var employee = await context.Employees.FindAsync(id);
        if (employee is null)
        {
            return NotFound();
        }

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

        if (await context.Employees.AnyAsync(e => e.Email == employee.Email && e.Id != employee.Id))
        {
            ModelState.AddModelError(nameof(employee.Email), "Já existe um colaborador com este email.");
        }

        if (!ModelState.IsValid)
        {
            return View(employee);
        }

        var existingEmployee = await context.Employees.FindAsync(id);
        if (existingEmployee is null)
        {
            return NotFound();
        }

        existingEmployee.FullName = employee.FullName;
        existingEmployee.Email = employee.Email;
        existingEmployee.JobTitle = employee.JobTitle;
        existingEmployee.IsActive = employee.IsActive;
        existingEmployee.IsAdmin = employee.IsAdmin;

        if (!string.IsNullOrWhiteSpace(employee.Password))
        {
            existingEmployee.PasswordHash = PasswordHasher.Hash(employee.Password);
        }

        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Colaborador atualizado.";

        return RedirectToAction(nameof(Index));
    }
}
