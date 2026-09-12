using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.Interfaces.Repositories;

namespace Fluxo.Application.UseCases.Portal;

public sealed class ListWorkspaceTelemetryRejectionsUseCase(
    GetAuthorizedWorkspaceUseCase authorizeWorkspace,
    ITelemetryIngestionRejectionRepository repository)
{
    public async Task<TelemetryRejectionPageResponse> ExecuteAsync(
        Guid userId, Guid workspaceId, int page, int pageSize, string? search,
        CancellationToken cancellationToken = default)
    {
        if (page < 1) throw new ArgumentOutOfRangeException(nameof(page), "Page must be greater than zero.");
        if (pageSize is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(pageSize), "PageSize must be between 1 and 100.");

        var workspace = await authorizeWorkspace.ExecuteAsync(userId, workspaceId, cancellationToken);
        var (records, total) = await repository.ListByTenantWorkspaceAsync(
            workspace.TenantId, workspace.Id, page, pageSize, search, cancellationToken);

        var items = records.Select(record => new TelemetryRejectionItemResponse(
            record.Id,
            record.ReceivedAtUtc,
            record.Topic,
            Preview(record.PayloadRaw),
            record.ErrorType.ToString(),
            record.Reason,
            record.DeviceId,
            record.MessageType,
            record.Sequence,
            record.Reprocessed,
            record.ReprocessAttempts)).ToArray();

        return new TelemetryRejectionPageResponse(items, page, pageSize, total);
    }

    private static string Preview(string payload)
    {
        if (string.IsNullOrWhiteSpace(payload)) return string.Empty;
        var singleLine = string.Join(" ", payload.Split(default(string[]), StringSplitOptions.RemoveEmptyEntries));
        return singleLine.Length <= 240 ? singleLine : singleLine[..240] + "…";
    }
}
