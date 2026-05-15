using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Security;
using Timesheets.Web.Services;
using Timesheets.Web.ViewModels;

namespace Timesheets.Web.Controllers;

public class AccountController(
    AppDbContext context,
    IPasswordResetService passwordResetService,
    INotificationService notificationService) : Controller
{
    [AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToLocal(returnUrl);
        }

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var employee = await context.Employees
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Email == model.Email && e.IsActive);

        if (employee is null || !PasswordHasher.Verify(model.Password, employee.PasswordHash))
        {
            ModelState.AddModelError(string.Empty, "Email ou password inválidos.");
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, employee.Id.ToString()),
            new(ClaimTypes.Name, employee.FullName),
            new(ClaimTypes.Email, employee.Email),
            new(ClaimTypes.Role, AppRoles.Employee)
        };

        if (employee.IsManager || employee.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AppRoles.Manager));
        }

        if (employee.IsAdmin)
        {
            claims.Add(new Claim(ClaimTypes.Role, AppRoles.Admin));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));

        return RedirectToLocal(model.ReturnUrl);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult ForgotPassword()
    {
        return View(new ForgotPasswordViewModel());
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var employee = await context.Employees
            .Include(item => item.Responsible)
            .FirstOrDefaultAsync(item => item.Email == model.Email && item.IsActive);

        if (employee is not null)
        {
            var token = await passwordResetService.CreateTokenAsync(employee);
            var resetUrl = Url.Action(nameof(ResetPassword), "Account", new { token }, Request.Scheme);
            var managePath = Url.Action("ChangePassword", "Employees", new { id = employee.Id });
            var manageUrl = Url.Action("ChangePassword", "Employees", new { id = employee.Id }, Request.Scheme);

            await notificationService.NotifyAsync(
                employee.Id,
                AppNotificationType.PasswordReset,
                "Reposição de password",
                $"Foi pedido um link para repor a tua password no TimeFlow.\n\nLink: {resetUrl}\n\nEste link expira em 2 horas.",
                resetUrl,
                sendEmail: true,
                emailSubject: "TimeFlow · Reposição de password");

            await NotifyPasswordResetResponsiblesAsync(employee, managePath, manageUrl);
        }

        TempData["StatusMessage"] = "Se o email existir, enviámos um link de reposição.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public async Task<IActionResult> ResetPassword(string token)
    {
        var resetToken = await passwordResetService.GetValidTokenAsync(token);
        if (resetToken is null)
        {
            TempData["StatusMessage"] = "O link de reposição é inválido ou expirou.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        return View(new ResetPasswordViewModel { Token = token });
    }

    [HttpPost]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var resetToken = await passwordResetService.GetValidTokenAsync(model.Token);
        if (resetToken?.Employee is null)
        {
            TempData["StatusMessage"] = "O link de reposição é inválido ou expirou.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var employeeId = resetToken.EmployeeId;
        var changed = await passwordResetService.ResetPasswordAsync(model.Token, model.Password);
        if (!changed)
        {
            TempData["StatusMessage"] = "O link de reposição é inválido ou expirou.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        await notificationService.NotifyAsync(
            employeeId,
            AppNotificationType.PasswordChanged,
            "Password alterada",
            "A tua password do TimeFlow foi alterada.",
            Url.Action(nameof(Login), "Account"),
            sendEmail: true,
            emailSubject: "TimeFlow · Password alterada");

        TempData["StatusMessage"] = "Password alterada. Já podes entrar.";
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied()
    {
        return View();
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    private async Task NotifyPasswordResetResponsiblesAsync(Employee employee, string? managePath, string? manageUrl)
    {
        var recipients = new List<Employee>();

        if (employee.Responsible is { IsActive: true } responsible &&
            (responsible.IsManager || responsible.IsAdmin))
        {
            recipients.Add(responsible);
        }
        else
        {
            recipients = await context.Employees
                .AsNoTracking()
                .Where(item => item.IsActive && item.IsAdmin)
                .OrderBy(item => item.FullName)
                .ToListAsync();
        }

        foreach (var recipient in recipients.DistinctBy(item => item.Id))
        {
            await notificationService.NotifyAsync(
                recipient.Id,
                AppNotificationType.PasswordResetRequest,
                "Pedido de reposição",
                $"{employee.FullName} pediu reposição de password. Podes alterar diretamente no centro de reposição.\n\nAbrir: {manageUrl}",
                managePath,
                sendEmail: true,
                emailSubject: "TimeFlow · Pedido de reposição");
        }
    }
}
