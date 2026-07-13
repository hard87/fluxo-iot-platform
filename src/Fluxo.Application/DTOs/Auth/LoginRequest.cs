using System.ComponentModel.DataAnnotations;

namespace Fluxo.Application.DTOs.Auth;

public sealed class LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(1)]
    [MaxLength(128)]
    public string Password { get; set; } = string.Empty;
}
