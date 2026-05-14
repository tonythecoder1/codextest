using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Models;
using Timesheets.Web.Security;

namespace Timesheets.Web.Data;

public static class SeedData
{
    public static async Task InitializeAsync(AppDbContext context)
    {
        if (await context.Employees.AnyAsync())
        {
            var admin = await context.Employees.FirstOrDefaultAsync(e => e.Email == "admin@timesheets.local");
            if (admin is null)
            {
                context.Employees.Add(new Employee
                {
                    FullName = "Administrador",
                    Email = "admin@timesheets.local",
                    JobTitle = "Admin",
                    IsAdmin = true,
                    PasswordHash = PasswordHasher.Hash("admin123")
                });
            }
            else
            {
                admin.IsAdmin = true;
            }

            var secondaryAdmin = await context.Employees.FirstOrDefaultAsync(e => e.Email == "admin@admin.com");
            if (secondaryAdmin is null)
            {
                context.Employees.Add(new Employee
                {
                    FullName = "Admin",
                    Email = "admin@admin.com",
                    JobTitle = "Admin",
                    IsAdmin = true,
                    PasswordHash = PasswordHasher.Hash("admin")
                });
            }
            else
            {
                secondaryAdmin.IsAdmin = true;
                secondaryAdmin.PasswordHash = PasswordHasher.Hash("admin");
            }

            var testUser = await context.Employees.FirstOrDefaultAsync(e => e.Email == "teste@teste.com");
            if (testUser is null)
            {
                context.Employees.Add(new Employee
                {
                    FullName = "Utilizador Teste",
                    Email = "teste@teste.com",
                    JobTitle = "Colaborador",
                    PasswordHash = PasswordHasher.Hash("teste")
                });
            }

            var employeesWithoutPassword = await context.Employees
                .Where(e => e.PasswordHash == string.Empty)
                .ToListAsync();

            foreach (var employee in employeesWithoutPassword)
            {
                employee.PasswordHash = PasswordHasher.Hash(employee.IsAdmin ? "admin123" : "password123");
            }

            await EnsureProjectWeekendSettingsAsync(context);
            await context.SaveChangesAsync();
            return;
        }

        var employees = new List<Employee>
        {
            new()
            {
                FullName = "Administrador",
                Email = "admin@timesheets.local",
                JobTitle = "Admin",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash("admin123")
            },
            new()
            {
                FullName = "Admin",
                Email = "admin@admin.com",
                JobTitle = "Admin",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash("admin")
            },
            new()
            {
                FullName = "Ana Ribeiro",
                Email = "ana.ribeiro@acme.local",
                JobTitle = "Product Designer",
                PasswordHash = PasswordHasher.Hash("password123")
            },
            new()
            {
                FullName = "Tiago Martins",
                Email = "tiago.martins@acme.local",
                JobTitle = "Backend Engineer",
                PasswordHash = PasswordHasher.Hash("password123")
            },
            new()
            {
                FullName = "Marta Silva",
                Email = "marta.silva@acme.local",
                JobTitle = "Project Manager",
                PasswordHash = PasswordHasher.Hash("password123")
            },
            new()
            {
                FullName = "Utilizador Teste",
                Email = "teste@teste.com",
                JobTitle = "Colaborador",
                PasswordHash = PasswordHasher.Hash("teste")
            }
        };

        var projects = new List<Project>
        {
            new() { Name = "Portal do Cliente", ClientName = "Northwind", ColorHex = "#0F766E" },
            new() { Name = "App Mobile", ClientName = "Contoso", ColorHex = "#DC2626", AllowWeekendWork = true },
            new() { Name = "Operações Internas", ClientName = "Interno", ColorHex = "#7C3AED", AllowWeekendWork = true }
        };

        await context.Employees.AddRangeAsync(employees);
        await context.Projects.AddRangeAsync(projects);
        await context.SaveChangesAsync();

        var today = DateTime.Today;
        var entries = new List<TimesheetEntry>
        {
            new()
            {
                EmployeeId = employees[1].Id,
                ProjectId = projects[0].Id,
                WorkDate = today.AddDays(-2),
                Hours = 6.5m,
                Description = "Wireframes e revisão do fluxo de aprovação",
                IsBillable = true
            },
            new()
            {
                EmployeeId = employees[2].Id,
                ProjectId = projects[1].Id,
                WorkDate = today.AddDays(-1),
                Hours = 7.5m,
                Description = "API de submissão de horas e testes de integração",
                IsBillable = true
            },
            new()
            {
                EmployeeId = employees[3].Id,
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

    private static async Task EnsureProjectWeekendSettingsAsync(AppDbContext context)
    {
        var appMobile = await context.Projects.FirstOrDefaultAsync(p => p.Name == "App Mobile");
        if (appMobile is not null)
        {
            appMobile.AllowWeekendWork = true;
        }

        var internalOps = await context.Projects.FirstOrDefaultAsync(p => p.Name == "Operações Internas");
        if (internalOps is not null)
        {
            internalOps.AllowWeekendWork = true;
        }
    }
}
