using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.ViewModels;

public class PasswordCenterViewModel
{
    public List<PasswordCenterEmployeeViewModel> Employees { get; set; } = [];
}

public class PasswordCenterEmployeeViewModel
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string JobTitle { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class AdminPasswordChangeViewModel
{
    public int EmployeeId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [MinLength(4)]
    [Display(Name = "Nova password")]
    public string Password { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "As passwords não coincidem.")]
    [Display(Name = "Confirmar password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}
