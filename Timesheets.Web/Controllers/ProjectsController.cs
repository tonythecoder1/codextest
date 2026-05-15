using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Security;

namespace Timesheets.Web.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class ProjectsController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var projects = await context.Projects
            .AsNoTracking()
            .Include(project => project.ResponsibleEmployees)
            .OrderByDescending(project => project.IsActive)
            .ThenBy(project => project.Name)
            .ToListAsync();

        return View(projects);
    }

    public async Task<IActionResult> Create()
    {
        var project = new Project();
        await PopulateFormOptionsAsync(project);
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project project)
    {
        if (await context.Projects.AnyAsync(item => item.Name == project.Name))
        {
            ModelState.AddModelError(nameof(project.Name), "Já existe um projeto com este nome.");
        }

        await ValidateProjectResponsiblesAsync(project.ResponsibleIds);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(project);
            return View(project);
        }

        project.ResponsibleEmployees = await context.Employees
            .Where(employee => project.ResponsibleIds.Contains(employee.Id))
            .ToListAsync();

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Projeto criado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var project = await context.Projects
            .Include(item => item.ResponsibleEmployees)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (project is null)
        {
            return NotFound();
        }

        project.ResponsibleIds = project.ResponsibleEmployees
            .OrderBy(employee => employee.FullName)
            .Select(employee => employee.Id)
            .ToList();

        await PopulateFormOptionsAsync(project);
        return View(project);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, Project project)
    {
        if (id != project.Id)
        {
            return NotFound();
        }

        if (await context.Projects.AnyAsync(item => item.Name == project.Name && item.Id != project.Id))
        {
            ModelState.AddModelError(nameof(project.Name), "Já existe um projeto com este nome.");
        }

        await ValidateProjectResponsiblesAsync(project.ResponsibleIds);

        if (!ModelState.IsValid)
        {
            await PopulateFormOptionsAsync(project);
            return View(project);
        }

        var existingProject = await context.Projects
            .Include(item => item.ResponsibleEmployees)
            .FirstOrDefaultAsync(item => item.Id == id);

        if (existingProject is null)
        {
            return NotFound();
        }

        existingProject.Name = project.Name;
        existingProject.ClientName = project.ClientName;
        existingProject.ColorHex = project.ColorHex;
        existingProject.IsActive = project.IsActive;
        existingProject.AllowWeekendWork = project.AllowWeekendWork;

        existingProject.ResponsibleEmployees.Clear();
        var responsibleEmployees = await context.Employees
            .Where(employee => project.ResponsibleIds.Contains(employee.Id))
            .ToListAsync();

        foreach (var employee in responsibleEmployees)
        {
            existingProject.ResponsibleEmployees.Add(employee);
        }

        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Projeto atualizado.";

        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateFormOptionsAsync(Project project)
    {
        ViewBag.ResponsibleOptions = await context.Employees
            .AsNoTracking()
            .Where(employee => employee.IsActive && (employee.IsManager || employee.IsAdmin))
            .OrderBy(employee => employee.FullName)
            .Select(employee => new SelectListItem
            {
                Value = employee.Id.ToString(),
                Text = employee.FullName,
                Selected = project.ResponsibleIds.Contains(employee.Id)
            })
            .ToListAsync();
    }

    private async Task ValidateProjectResponsiblesAsync(List<int> responsibleIds)
    {
        if (responsibleIds.Count == 0)
        {
            return;
        }

        var validIds = await context.Employees
            .AsNoTracking()
            .Where(employee => responsibleIds.Contains(employee.Id) && employee.IsActive && (employee.IsManager || employee.IsAdmin))
            .Select(employee => employee.Id)
            .ToListAsync();

        if (validIds.Count != responsibleIds.Distinct().Count())
        {
            ModelState.AddModelError(nameof(Project.ResponsibleIds), "Seleciona apenas responsáveis ativos.");
        }
    }
}
