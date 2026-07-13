using System.Text.RegularExpressions;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Auth;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Services;
using Fluxo.Domain.Entities;

namespace Fluxo.Application.UseCases.Auth;

public sealed partial class RegisterPlatformUserUseCase
{
    private readonly IPlatformUserRepository _userRepository;
    private readonly IUserPasswordService _passwordService;

    public RegisterPlatformUserUseCase(
        IPlatformUserRepository userRepository,
        IUserPasswordService passwordService)
    {
        _userRepository = userRepository;
        _passwordService = passwordService;
    }

    public async Task<RegisterUserResponse> ExecuteAsync(
        RegisterUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null)
            throw new ValidationException("Request is required.");

        var email = request.Email?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException("Email is required.");

        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Password is required.");

        EnsurePasswordPolicy(password);

        var normalizedEmail = PlatformUser.NormalizeEmail(email);
        var existing = await _userRepository.GetByEmailNormalizedAsync(normalizedEmail, cancellationToken);

        if (existing is not null)
            throw new ConflictException("Unable to register with provided credentials.");

        var hashMaterial = _passwordService.HashPassword(password);
        var user = new PlatformUser(email, hashMaterial.PasswordHash, hashMaterial.PasswordSalt);
        await _userRepository.AddAsync(user, cancellationToken);

        return new RegisterUserResponse
        {
            UserId = user.Id,
            Email = user.Email,
            CreatedAtUtc = user.CreatedAtUtc
        };
    }

    private static void EnsurePasswordPolicy(string password)
    {
        if (password.Length < 12)
            throw new ValidationException("Password must be at least 12 characters.");

        if (!PasswordLowerPattern().IsMatch(password) ||
            !PasswordUpperPattern().IsMatch(password) ||
            !PasswordDigitPattern().IsMatch(password) ||
            !PasswordSymbolPattern().IsMatch(password))
        {
            throw new ValidationException("Password must contain upper, lower, digit and symbol.");
        }
    }

    [GeneratedRegex("[a-z]", RegexOptions.Compiled)]
    private static partial Regex PasswordLowerPattern();

    [GeneratedRegex("[A-Z]", RegexOptions.Compiled)]
    private static partial Regex PasswordUpperPattern();

    [GeneratedRegex("\\d", RegexOptions.Compiled)]
    private static partial Regex PasswordDigitPattern();

    [GeneratedRegex("[^\\w\\s]", RegexOptions.Compiled)]
    private static partial Regex PasswordSymbolPattern();
}
