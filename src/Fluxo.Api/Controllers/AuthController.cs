using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.UseCases.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Fluxo.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly RegisterPlatformUserUseCase _registerPlatformUserUseCase;
    private readonly LoginPlatformUserUseCase _loginPlatformUserUseCase;
    private readonly GetAuthenticatedUserUseCase _getAuthenticatedUserUseCase;

    public AuthController(
        RegisterPlatformUserUseCase registerPlatformUserUseCase,
        LoginPlatformUserUseCase loginPlatformUserUseCase,
        GetAuthenticatedUserUseCase getAuthenticatedUserUseCase)
    {
        _registerPlatformUserUseCase = registerPlatformUserUseCase;
        _loginPlatformUserUseCase = loginPlatformUserUseCase;
        _getAuthenticatedUserUseCase = getAuthenticatedUserUseCase;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Register(
        [FromBody] RegisterUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _registerPlatformUserUseCase.ExecuteAsync(request, cancellationToken);
        return Created(string.Empty, result);
    }

    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _loginPlatformUserUseCase.ExecuteAsync(request, cancellationToken);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetRequiredUserId();
        var result = await _getAuthenticatedUserUseCase.ExecuteAsync(userId, cancellationToken);
        return Ok(result);
    }
}
