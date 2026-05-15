using Timesheets.Web.Models;

namespace Timesheets.Web.Services;

public interface IPasswordResetService
{
    Task<string> CreateTokenAsync(Employee employee, int? createdByEmployeeId = null);
    Task<PasswordResetToken?> GetValidTokenAsync(string token);
    Task<bool> ResetPasswordAsync(string token, string password);
}
