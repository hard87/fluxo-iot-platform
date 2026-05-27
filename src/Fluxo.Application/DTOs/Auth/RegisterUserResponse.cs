namespace Fluxo.Application.DTOs.Auth;

public sealed class RegisterUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}
