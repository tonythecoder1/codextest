using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;

namespace Timesheets.Web.Controllers;

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
        if (await context.Employees.AnyAsync(e => e.Email == employee.Email))
        {
            ModelState.AddModelError(nameof(employee.Email), "Já existe um colaborador com este email.");
        }

        if (!ModelState.IsValid)
        {
            return View(employee);
        }

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

        context.Update(employee);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Colaborador atualizado.";

        return RedirectToAction(nameof(Index));
    }
}
