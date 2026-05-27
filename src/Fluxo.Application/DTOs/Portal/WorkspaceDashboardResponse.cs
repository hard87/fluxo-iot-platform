namespace Fluxo.Application.DTOs.Portal;

public sealed class WorkspaceDashboardResponse
{
    public Guid WorkspaceId { get; set; }
    public string TenantId { get; set; } = string.Empty;
    public int DevicesTotal { get; set; }
    public int DevicesOnline { get; set; }
    public int DevicesOffline { get; set; }
    public int DevicesUnknown { get; set; }
    public DateTime? LastTelemetryReceivedAtUtc { get; set; }
    public long MessagesProcessed { get; set; }
    public long MessagesRejected { get; set; }
}
