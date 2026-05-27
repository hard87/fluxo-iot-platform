using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Auth;

public sealed class GetAuthenticatedUserUseCase
{
    private readonly IPlatformUserRepository _userRepository;

    public GetAuthenticatedUserUseCase(IPlatformUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AuthenticatedUserResponse> ExecuteAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ValidationException("User id is required.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("User is not authenticated.");

        return new AuthenticatedUserResponse
        {
            UserId = user.Id,
            Email = user.Email
        };
    }
}
