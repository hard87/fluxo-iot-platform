using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Fluxo.Application.Common.Exceptions;
using Fluxo.Application.DTOs.Workspaces;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;

namespace Fluxo.Application.UseCases.Workspaces;

public sealed partial class CreateWorkspaceUseCase
{
    private readonly IPlatformUserRepository _userRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;

    public CreateWorkspaceUseCase(
        IPlatformUserRepository userRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository)
    {
        _userRepository = userRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
    }

    public async Task<WorkspaceResponse> ExecuteAsync(
        Guid userId,
        CreateWorkspaceRequest request,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        if (request is null)
            throw new ValidationException("Request is required.");

        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        if (user is null || !user.IsActive)
            throw new UnauthorizedException("User is not authenticated.");

        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(name))
            throw new ValidationException("Workspace name is required.");

        var tenantId = BuildTenantId(request.TenantId, name);
        var existingTenant = await _workspaceRepository.GetByTenantIdAsync(tenantId, cancellationToken);
        if (existingTenant is not null)
            throw new ConflictException("Workspace tenant is already in use.");

        var workspace = new Workspace(tenantId, name);
        await _workspaceRepository.AddAsync(workspace, cancellationToken);

        var membership = new WorkspaceMembership(workspace.Id, userId, WorkspaceMembershipRole.Owner);
        await _workspaceMembershipRepository.AddAsync(membership, cancellationToken);

        return new WorkspaceResponse
        {
            Id = workspace.Id,
            TenantId = workspace.TenantId,
            Name = workspace.Name,
            Role = WorkspaceMembershipRole.Owner,
            CreatedAtUtc = workspace.CreatedAtUtc
        };
    }

    private static string BuildTenantId(string? requestedTenantId, string workspaceName)
    {
        var baseText = string.IsNullOrWhiteSpace(requestedTenantId) ? workspaceName : requestedTenantId.Trim();

        var normalized = RemoveDiacritics(baseText).ToLowerInvariant();
        normalized = InvalidTenantPattern().Replace(normalized, "-");
        normalized = MultiDashPattern().Replace(normalized, "-").Trim('-');

        if (normalized.Length < 3)
            throw new ValidationException("TenantId must contain at least 3 valid characters.");

        if (normalized.Length > 120)
            normalized = normalized[..120].Trim('-');

        return normalized;
    }

    private static string RemoveDiacritics(string value)
    {
        var normalizedString = value.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalizedString.Length);

        foreach (var c in normalizedString)
        {
            var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
            if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    [GeneratedRegex("[^a-z0-9\\-]+", RegexOptions.Compiled)]
    private static partial Regex InvalidTenantPattern();

    [GeneratedRegex("-{2,}", RegexOptions.Compiled)]
    private static partial Regex MultiDashPattern();
}
