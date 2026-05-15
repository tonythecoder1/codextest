using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Timesheets.Web.Data;
using Timesheets.Web.Models;
using Timesheets.Web.Security;

namespace Timesheets.Web.Services;

public class PasswordResetService(AppDbContext context) : IPasswordResetService
{
    public async Task<string> CreateTokenAsync(Employee employee, int? createdByEmployeeId = null)
    {
        var token = CreatePlainToken();
        context.PasswordResetTokens.Add(new PasswordResetToken
        {
            EmployeeId = employee.Id,
            TokenHash = HashToken(token),
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(2),
            CreatedByEmployeeId = createdByEmployeeId
        });

        await context.SaveChangesAsync();
        return token;
    }

    public async Task<PasswordResetToken?> GetValidTokenAsync(string token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        var tokenHash = HashToken(token);
        return await context.PasswordResetTokens
            .Include(item => item.Employee)
            .FirstOrDefaultAsync(item =>
                item.TokenHash == tokenHash &&
                item.UsedAt == null &&
                item.ExpiresAt >= DateTime.UtcNow &&
                item.Employee != null &&
                item.Employee.IsActive);
    }

    public async Task<bool> ResetPasswordAsync(string token, string password)
    {
        var resetToken = await GetValidTokenAsync(token);
        if (resetToken?.Employee is null)
        {
            return false;
        }

        resetToken.Employee.PasswordHash = PasswordHasher.Hash(password);
        resetToken.UsedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return true;
    }

    private static string CreatePlainToken()
    {
        return Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes);
    }
}
