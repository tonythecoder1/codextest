using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;

namespace Timesheets.Web.Controllers;

public class ProjectsController(AppDbContext context) : Controller
{
    public async Task<IActionResult> Index()
    {
        var projects = await context.Projects
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return View(projects);
    }

    public IActionResult Create()
    {
        return View(new Project());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(Project project)
    {
        if (await context.Projects.AnyAsync(p => p.Name == project.Name))
        {
            ModelState.AddModelError(nameof(project.Name), "Já existe um projeto com este nome.");
        }

        if (!ModelState.IsValid)
        {
            return View(project);
        }

        context.Projects.Add(project);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Projeto criado com sucesso.";

        return RedirectToAction(nameof(Index));
    }

    public async Task<IActionResult> Edit(int id)
    {
        var project = await context.Projects.FindAsync(id);
        if (project is null)
        {
            return NotFound();
        }

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

        if (await context.Projects.AnyAsync(p => p.Name == project.Name && p.Id != project.Id))
        {
            ModelState.AddModelError(nameof(project.Name), "Já existe um projeto com este nome.");
        }

        if (!ModelState.IsValid)
        {
            return View(project);
        }

        context.Update(project);
        await context.SaveChangesAsync();
        TempData["StatusMessage"] = "Projeto atualizado.";

        return RedirectToAction(nameof(Index));
    }
}
