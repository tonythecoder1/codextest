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
            await EnsureSeedUsersAsync(context);
            await EnsureProjectWeekendSettingsAsync(context);
            await EnsureProjectResponsiblesAsync(context);
            await EnsureResponsibleAssignmentsAsync(context);
            await context.SaveChangesAsync();
            return;
        }

        var admin = new Employee
        {
            FullName = "Administrador",
            Email = "admin@timesheets.local",
            JobTitle = "Admin",
            IsAdmin = true,
            PasswordHash = PasswordHasher.Hash("admin123")
        };

        var secondaryAdmin = new Employee
        {
            FullName = "Admin",
            Email = "admin@admin.com",
            JobTitle = "Admin",
            IsAdmin = true,
            PasswordHash = PasswordHasher.Hash("admin")
        };

        var manager = new Employee
        {
            FullName = "Marta Silva",
            Email = "marta.silva@acme.local",
            JobTitle = "Responsável",
            IsManager = true,
            PasswordHash = PasswordHasher.Hash("password123")
        };

        var ana = new Employee
        {
            FullName = "Ana Ribeiro",
            Email = "ana.ribeiro@acme.local",
            JobTitle = "Product Designer",
            PasswordHash = PasswordHasher.Hash("password123"),
            Responsible = manager
        };

        var tiago = new Employee
        {
            FullName = "Tiago Martins",
            Email = "tiago.martins@acme.local",
            JobTitle = "Backend Engineer",
            PasswordHash = PasswordHasher.Hash("password123"),
            Responsible = manager
        };

        var testUser = new Employee
        {
            FullName = "Utilizador Teste",
            Email = "teste@teste.com",
            JobTitle = "Colaborador",
            PasswordHash = PasswordHasher.Hash("teste"),
            Responsible = manager
        };

        var employees = new List<Employee>
        {
            admin,
            secondaryAdmin,
            manager,
            ana,
            tiago,
            testUser
        };

        var clientPortal = new Project
        {
            Name = "Portal do Cliente",
            ClientName = "Northwind",
            ColorHex = "#0F766E",
            ResponsibleEmployees = [manager]
        };

        var mobileApp = new Project
        {
            Name = "App Mobile",
            ClientName = "Contoso",
            ColorHex = "#DC2626",
            AllowWeekendWork = true,
            ResponsibleEmployees = [manager]
        };

        var internalOps = new Project
        {
            Name = "Operações Internas",
            ClientName = "Interno",
            ColorHex = "#7C3AED",
            AllowWeekendWork = true,
            ResponsibleEmployees = [manager, admin]
        };

        var projects = new List<Project> { clientPortal, mobileApp, internalOps };

        await context.Employees.AddRangeAsync(employees);
        await context.Projects.AddRangeAsync(projects);
        await context.SaveChangesAsync();

        var today = DateTime.Today;
        var entries = new List<TimesheetEntry>
        {
            new()
            {
                EmployeeId = ana.Id,
                ProjectId = clientPortal.Id,
                WorkDate = today.AddDays(-2),
                Hours = 6.5m,
                Description = "Wireframes e revisão do fluxo de aprovação",
                IsBillable = true,
                ApprovalStatus = TimesheetApprovalStatus.Approved,
                ApprovedByEmployeeId = manager.Id,
                ApprovedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                EmployeeId = tiago.Id,
                ProjectId = mobileApp.Id,
                WorkDate = today.AddDays(-1),
                Hours = 7.5m,
                Description = "API de submissão de horas e testes de integração",
                IsBillable = true,
                ApprovalStatus = TimesheetApprovalStatus.Approved,
                ApprovedByEmployeeId = manager.Id,
                ApprovedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                EmployeeId = manager.Id,
                ProjectId = internalOps.Id,
                WorkDate = today,
                Hours = 3m,
                Description = "Planeamento semanal e alinhamento com stakeholders",
                IsBillable = false,
                ApprovalStatus = TimesheetApprovalStatus.Approved,
                ApprovedByEmployeeId = admin.Id,
                ApprovedAt = DateTime.UtcNow
            }
        };

        await context.TimesheetEntries.AddRangeAsync(entries);
        await context.SaveChangesAsync();
    }

    private static async Task EnsureSeedUsersAsync(AppDbContext context)
    {
        var admin = await EnsureEmployeeAsync(
            context,
            "admin@timesheets.local",
            () => new Employee
            {
                FullName = "Administrador",
                Email = "admin@timesheets.local",
                JobTitle = "Admin",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash("admin123")
            });

        admin.IsAdmin = true;

        var secondaryAdmin = await EnsureEmployeeAsync(
            context,
            "admin@admin.com",
            () => new Employee
            {
                FullName = "Admin",
                Email = "admin@admin.com",
                JobTitle = "Admin",
                IsAdmin = true,
                PasswordHash = PasswordHasher.Hash("admin")
            });

        secondaryAdmin.IsAdmin = true;
        secondaryAdmin.PasswordHash = PasswordHasher.Hash("admin");

        var manager = await EnsureEmployeeAsync(
            context,
            "marta.silva@acme.local",
            () => new Employee
            {
                FullName = "Marta Silva",
                Email = "marta.silva@acme.local",
                JobTitle = "Responsável",
                IsManager = true,
                PasswordHash = PasswordHasher.Hash("password123")
            });

        manager.IsManager = true;
        manager.IsActive = true;

        var testUser = await EnsureEmployeeAsync(
            context,
            "teste@teste.com",
            () => new Employee
            {
                FullName = "Utilizador Teste",
                Email = "teste@teste.com",
                JobTitle = "Colaborador",
                PasswordHash = PasswordHasher.Hash("teste")
            });

        testUser.PasswordHash = PasswordHasher.Hash("teste");

        var employeesWithoutPassword = await context.Employees
            .Where(employee => employee.PasswordHash == string.Empty)
            .ToListAsync();

        foreach (var employee in employeesWithoutPassword)
        {
            employee.PasswordHash = PasswordHasher.Hash(employee.IsAdmin ? "admin123" : "password123");
        }
    }

    private static async Task<Employee> EnsureEmployeeAsync(AppDbContext context, string email, Func<Employee> factory)
    {
        var employee = await context.Employees.FirstOrDefaultAsync(item => item.Email == email);
        if (employee is not null)
        {
            return employee;
        }

        employee = factory();
        context.Employees.Add(employee);
        await context.SaveChangesAsync();
        return employee;
    }

    private static async Task EnsureResponsibleAssignmentsAsync(AppDbContext context)
    {
        var managerId = await context.Employees
            .Where(employee => employee.Email == "marta.silva@acme.local")
            .Select(employee => (int?)employee.Id)
            .FirstOrDefaultAsync();

        if (!managerId.HasValue)
        {
            managerId = await context.Employees
                .Where(employee => employee.Email == "admin@admin.com")
                .Select(employee => (int?)employee.Id)
                .FirstOrDefaultAsync();
        }

        if (!managerId.HasValue)
        {
            return;
        }

        var employeesToAssign = await context.Employees
            .Where(employee =>
                !employee.IsAdmin &&
                !employee.IsManager &&
                !employee.ResponsibleId.HasValue)
            .ToListAsync();

        foreach (var employee in employeesToAssign)
        {
            employee.ResponsibleId = managerId.Value;
        }
    }

    private static async Task EnsureProjectResponsiblesAsync(AppDbContext context)
    {
        var defaultResponsibleIds = await context.Employees
            .Where(employee => employee.IsActive && (employee.IsManager || employee.IsAdmin))
            .OrderByDescending(employee => employee.IsManager)
            .ThenBy(employee => employee.FullName)
            .Select(employee => employee.Id)
            .Take(2)
            .ToListAsync();

        if (defaultResponsibleIds.Count == 0)
        {
            return;
        }

        var projects = await context.Projects
            .Include(project => project.ResponsibleEmployees)
            .ToListAsync();

        foreach (var project in projects.Where(project => project.ResponsibleEmployees.Count == 0))
        {
            var responsibleEmployees = await context.Employees
                .Where(employee => defaultResponsibleIds.Contains(employee.Id))
                .ToListAsync();

            foreach (var employee in responsibleEmployees)
            {
                project.ResponsibleEmployees.Add(employee);
            }
        }
    }

    private static async Task EnsureProjectWeekendSettingsAsync(AppDbContext context)
    {
        var appMobile = await context.Projects.FirstOrDefaultAsync(project => project.Name == "App Mobile");
        if (appMobile is not null)
        {
            appMobile.AllowWeekendWork = true;
        }

        var internalOps = await context.Projects.FirstOrDefaultAsync(project => project.Name == "Operações Internas");
        if (internalOps is not null)
        {
            internalOps.AllowWeekendWork = true;
        }
    }
}
