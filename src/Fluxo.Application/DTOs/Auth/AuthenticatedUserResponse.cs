namespace Fluxo.Application.DTOs.Auth;

public sealed class AuthenticatedUserResponse
{
    public Guid UserId { get; set; }
    public string Email { get; set; } = string.Empty;
}
