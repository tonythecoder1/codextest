using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Models;

namespace Timesheets.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        if (await context.Employees.AnyAsync())
        {
            return;
        }

        var employees = new List<Employee>
        {
            new() { FullName = "Ana Ribeiro", Email = "ana.ribeiro@acme.local", JobTitle = "Product Designer" },
            new() { FullName = "Tiago Martins", Email = "tiago.martins@acme.local", JobTitle = "Backend Engineer" },
            new() { FullName = "Marta Silva", Email = "marta.silva@acme.local", JobTitle = "Project Manager" }
        };

        var projects = new List<Project>
        {
            new() { Name = "Portal do Cliente", ClientName = "Northwind", ColorHex = "#0F766E" },
            new() { Name = "App Mobile", ClientName = "Contoso", ColorHex = "#DC2626" },
            new() { Name = "Operações Internas", ClientName = "Interno", ColorHex = "#7C3AED" }
        };

        await context.Employees.AddRangeAsync(employees);
        await context.Projects.AddRangeAsync(projects);
        await context.SaveChangesAsync();

        var today = DateTime.Today;
        var entries = new List<TimesheetEntry>
        {
            new()
            {
                EmployeeId = employees[0].Id,
                ProjectId = projects[0].Id,
                WorkDate = today.AddDays(-2),
                Hours = 6.5m,
                Description = "Wireframes e revisão do fluxo de aprovação",
                IsBillable = true
            },
            new()
            {
                EmployeeId = employees[1].Id,
                ProjectId = projects[1].Id,
                WorkDate = today.AddDays(-1),
                Hours = 7.5m,
                Description = "API de submissão de horas e testes de integração",
                IsBillable = true
            },
            new()
            {
                EmployeeId = employees[2].Id,
                ProjectId = projects[2].Id,
                WorkDate = today,
                Hours = 3m,
                Description = "Planeamento semanal e alinhamento com stakeholders",
                IsBillable = false
            }
        };

        await context.TimesheetEntries.AddRangeAsync(entries);
        await context.SaveChangesAsync();
    }
}
