using Fluxo.Worker.Ingestion.Models;

namespace Fluxo.Worker.Ingestion.Services;

public static class MqttTopicParser
{
    public static bool TryParseTelemetryTopic(string topic, out IngestionTopicContext? context)
    {
        context = null;

        if (string.IsNullOrWhiteSpace(topic))
            return false;

        var segments = topic.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (segments.Length != 8)
            return false;

        if (!segments[0].Equals("fluxo", StringComparison.OrdinalIgnoreCase) ||
            !segments[1].Equals("tenants", StringComparison.OrdinalIgnoreCase) ||
            !segments[3].Equals("workspaces", StringComparison.OrdinalIgnoreCase) ||
            !segments[5].Equals("devices", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Guid.TryParse(segments[4], out var workspaceId) || workspaceId == Guid.Empty)
            return false;

        var tenantId = segments[2];
        var deviceId = segments[6];
        var messageType = segments[7];

        if (string.IsNullOrWhiteSpace(tenantId) ||
            string.IsNullOrWhiteSpace(deviceId) ||
            string.IsNullOrWhiteSpace(messageType))
        {
            return false;
        }

        context = new IngestionTopicContext(
            tenantId.Trim(),
            workspaceId,
            deviceId.Trim(),
            messageType.Trim(),
            topic);

        return true;
    }
}
