namespace Fluxo.Worker.Ingestion.Models;

public sealed record IngestionTopicContext(
    string TenantId,
    Guid WorkspaceId,
    string DeviceId,
    string MessageType,
    string RawTopic);
