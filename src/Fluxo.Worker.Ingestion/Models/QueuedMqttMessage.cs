namespace Fluxo.Worker.Ingestion.Models;

public sealed record QueuedMqttMessage(
    string Topic,
    string PayloadJson,
    DateTime ReceivedAtUtc);
