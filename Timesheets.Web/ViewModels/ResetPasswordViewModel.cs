using System.ComponentModel.DataAnnotations;

namespace Timesheets.Web.ViewModels;

public class ResetPasswordViewModel
{
    [Required]
    public string Token { get; set; } = string.Empty;

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
