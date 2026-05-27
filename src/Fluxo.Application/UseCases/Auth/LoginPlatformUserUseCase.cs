using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Services;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Auth;

public sealed class LoginPlatformUserUseCase
{
    private readonly IPlatformUserRepository _userRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;
    private readonly IUserPasswordService _passwordService;
    private readonly IAccessTokenService _accessTokenService;

    public LoginPlatformUserUseCase(
        IPlatformUserRepository userRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository,
        IUserPasswordService passwordService,
        IAccessTokenService accessTokenService)
    {
        _userRepository = userRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
        _passwordService = passwordService;
        _accessTokenService = accessTokenService;
    }

    public async Task<LoginResponse> ExecuteAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ValidationException("Request is required.");

        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw InvalidCredentials();

        PlatformUser? user;
        try
        {
            var normalizedEmail = PlatformUser.NormalizeEmail(email);
            user = await _userRepository.GetByEmailNormalizedAsync(normalizedEmail, cancellationToken);
        }
        catch (ArgumentException)
        {
            throw InvalidCredentials();
        }

        if (user is null)
            throw InvalidCredentials();

        if (!user.IsActive)
            throw InvalidCredentials();

        var isValidPassword = _passwordService.VerifyPassword(
            password,
            user.PasswordHash,
            user.PasswordSalt);

        if (!isValidPassword)
            throw InvalidCredentials();

        var trackedUser = await _userRepository.GetTrackedByIdAsync(user.Id, cancellationToken);
        trackedUser?.RecordLogin(DateTime.UtcNow);
        if (trackedUser is not null)
            await _userRepository.UpdateAsync(trackedUser, cancellationToken);

        var memberships = await _workspaceMembershipRepository.GetByUserIdAsync(user.Id, cancellationToken);
        var token = _accessTokenService.Issue(
            new AccessTokenPayload(
                user.Id,
                user.Email,
                memberships.Select(x => x.WorkspaceId.ToString()).ToArray()));

        return new LoginResponse
        {
            AccessToken = token.AccessToken,
            ExpiresAtUtc = token.ExpiresAtUtc,
            User = new AuthenticatedUserResponse
            {
                UserId = user.Id,
                Email = user.Email
            }
        };
    }

    private static UnauthorizedException InvalidCredentials()
    {
        return new UnauthorizedException("Invalid credentials.");
    }
}
