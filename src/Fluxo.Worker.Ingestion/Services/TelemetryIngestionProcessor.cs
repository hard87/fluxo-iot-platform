using System.Globalization;
using System.Text.Json;
using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Application.Models;
using Fluxo.Domain.Entities;
using Fluxo.Domain.Enums;
using Fluxo.Worker.Ingestion.Models;
using Fluxo.Worker.Ingestion.Options;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;

namespace Fluxo.Worker.Ingestion.Services;

public class TelemetryIngestionProcessor : ITelemetryIngestionProcessor
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITelemetryIngestionRepository _telemetryIngestionRepository;
    private readonly ITelemetryIngestionRejectionRepository _rejectionRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<TelemetryIngestionProcessor> _logger;
    private readonly MqttIngestionOptions _options;

    public TelemetryIngestionProcessor(
        ITelemetryIngestionRepository telemetryIngestionRepository,
        ITelemetryIngestionRejectionRepository rejectionRepository,
        IDeviceRepository deviceRepository,
        IOptions<MqttIngestionOptions> options,
        ILogger<TelemetryIngestionProcessor> logger)
    {
        _telemetryIngestionRepository = telemetryIngestionRepository;
        _rejectionRepository = rejectionRepository;
        _deviceRepository = deviceRepository;
        _logger = logger;
        _options = options.Value;
    }

    public async Task<TelemetryIngestionProcessingResult> ProcessAsync(
        string topic,
        string payloadJson,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default)
    {
        IngestionTopicContext? topicContext = null;

        if (!MqttTopicParser.TryParseTelemetryTopic(topic, out topicContext))
        {
            const string reason = "Topic fora do padrao esperado de ingestao.";
            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.Validation,
                reason,
                cancellationToken);

            _logger.LogWarning("Mensagem rejeitada. Topic invalido: {Topic}", topic);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            const string reason = "Payload vazio.";
            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.PayloadInvalid,
                reason,
                cancellationToken,
                topicContext);

            _logger.LogWarning("Mensagem rejeitada. Payload vazio. Topic: {Topic}", topic);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        IncomingTelemetryMessage? message;
        try
        {
            message = JsonSerializer.Deserialize<IncomingTelemetryMessage>(payloadJson, JsonOptions);
        }
        catch (JsonException ex)
        {
            const string reason = "Payload JSON invalido.";
            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.PayloadInvalid,
                reason,
                cancellationToken,
                topicContext);

            _logger.LogWarning(ex, "Mensagem rejeitada. JSON invalido. Topic: {Topic}", topic);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        if (message is null)
        {
            const string reason = "Payload nao pode ser desserializado.";
            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.PayloadInvalid,
                reason,
                cancellationToken,
                topicContext);

            _logger.LogWarning("Mensagem rejeitada. Desserializacao nula. Topic: {Topic}", topic);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        var rejectionReasons = new List<string>();

        var schemaVersion = RequireText(message.SchemaVersion, "schemaVersion", rejectionReasons);
        var tenantId = RequireText(message.TenantId, "tenantId", rejectionReasons);
        var deviceId = RequireText(message.DeviceId, "deviceId", rejectionReasons);
        var messageType = RequireText(message.MessageType, "messageType", rejectionReasons);

        if (!TryParseWorkspaceId(message.WorkspaceId, out var workspaceId))
            rejectionReasons.Add("workspaceId ausente ou invalido.");

        if (!TryParseOccurredAtUtc(message.TimestampUtc, out var occurredAtUtc))
            rejectionReasons.Add("timestampUtc ausente ou invalido.");

        var isTelemetryMessage = string.Equals(
            messageType,
            "telemetry",
            StringComparison.OrdinalIgnoreCase);

        if (isTelemetryMessage && message.Metrics is null)
            rejectionReasons.Add("metrics nao pode ser nulo para messageType telemetry.");

        if (isTelemetryMessage && !message.Sequence.HasValue)
            rejectionReasons.Add("sequence e obrigatorio para messageType telemetry.");

        ValidateTopicPayloadConsistency(
            topicContext!,
            tenantId,
            workspaceId,
            deviceId,
            messageType,
            rejectionReasons);

        if (rejectionReasons.Count > 0)
        {
            var reason = string.Join(" ", rejectionReasons);
            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.Validation,
                reason,
                cancellationToken,
                topicContext,
                message);

            _logger.LogWarning("Mensagem rejeitada. Topic: {Topic}. Motivos: {Reasons}", topic, reason);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        var provisionedDevice = await _deviceRepository.GetTrackedByTenantWorkspaceAndIdentifierAsync(
            tenantId!,
            workspaceId,
            deviceId!,
            cancellationToken);

        if (provisionedDevice is null)
        {
            const string reason = "Dispositivo nao provisionado para tenant/workspace/device informado.";

            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.Validation,
                reason,
                cancellationToken,
                topicContext,
                message);

            _logger.LogWarning(
                "Mensagem rejeitada por dispositivo nao provisionado. Tenant: {TenantId}. Workspace: {WorkspaceId}. Device: {DeviceId}.",
                tenantId,
                workspaceId,
                deviceId);

            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        if (!provisionedDevice.IsActive)
        {
            const string reason = "Dispositivo inativo para ingestao de telemetria.";

            await PersistRejectionAsync(
                receivedAtUtc,
                topic,
                payloadJson,
                TelemetryIngestionFailureType.Validation,
                reason,
                cancellationToken,
                topicContext,
                message);

            _logger.LogWarning(
                "Mensagem rejeitada por dispositivo inativo. Tenant: {TenantId}. Workspace: {WorkspaceId}. Device: {DeviceId}.",
                tenantId,
                workspaceId,
                deviceId);

            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        var record = new TelemetryIngestionRecord(
            tenantId!,
            workspaceId,
            deviceId!,
            messageType!,
            topic,
            schemaVersion!,
            occurredAtUtc,
            receivedAtUtc,
            payloadJson,
            message.FirmwareVersion,
            message.Sequence,
            message.Metrics?.Temperature,
            message.Metrics?.Humidity,
            message.Metrics?.Battery,
            message.Metrics?.Rssi,
            message.Metrics?.UptimeSec);

        var retryCount = Math.Max(_options.DatabaseRetryCount, 1);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(_options.DatabaseRetryDelayMs, 10));

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                var writeResult = await _telemetryIngestionRepository.AddAsync(record, cancellationToken);

                if (writeResult == TelemetryIngestionWriteResult.Duplicate)
                {
                    const string duplicateReason = "Mensagem duplicada por tenant/workspace/device/sequence.";

                    await UpdateDeviceSnapshotBestEffortAsync(
                        provisionedDevice,
                        payloadJson,
                        receivedAtUtc,
                        occurredAtUtc,
                        message.Sequence,
                        cancellationToken);

                    await PersistRejectionAsync(
                        receivedAtUtc,
                        topic,
                        payloadJson,
                        TelemetryIngestionFailureType.Duplicate,
                        duplicateReason,
                        cancellationToken,
                        topicContext,
                        message);

                    _logger.LogInformation(
                        "Mensagem duplicada descartada. Tenant: {TenantId}. Workspace: {WorkspaceId}. Device: {DeviceId}. Sequence: {Sequence}.",
                        record.TenantId,
                        record.WorkspaceId,
                        record.DeviceId,
                        record.Sequence);

                    return TelemetryIngestionProcessingResult.Duplicate(duplicateReason);
                }

                _logger.LogInformation(
                    "Mensagem persistida. Tenant: {TenantId}. Workspace: {WorkspaceId}. Device: {DeviceId}. Type: {MessageType}. Sequence: {Sequence}.",
                    record.TenantId,
                    record.WorkspaceId,
                    record.DeviceId,
                    record.MessageType,
                    record.Sequence);

                await UpdateDeviceSnapshotBestEffortAsync(
                    provisionedDevice,
                    payloadJson,
                    receivedAtUtc,
                    occurredAtUtc,
                    message.Sequence,
                    cancellationToken);

                return TelemetryIngestionProcessingResult.Persisted();
            }
            catch (Exception ex) when (IsTransient(ex) && attempt < retryCount)
            {
                _logger.LogWarning(
                    ex,
                    "Falha transitoria ao persistir telemetria. Tentativa {Attempt}/{MaxAttempts}. Topic: {Topic}",
                    attempt,
                    retryCount,
                    topic);

                await Task.Delay(retryDelay, cancellationToken);
            }
            catch (Exception ex)
            {
                var failureType = IsTransient(ex)
                    ? TelemetryIngestionFailureType.TransientError
                    : TelemetryIngestionFailureType.DatabaseError;

                var reason = failureType == TelemetryIngestionFailureType.TransientError
                    ? "Falha transitoria ao persistir telemetria."
                    : "Falha ao persistir telemetria no banco.";

                await PersistRejectionAsync(
                    receivedAtUtc,
                    topic,
                    payloadJson,
                    failureType,
                    reason,
                    cancellationToken,
                    topicContext,
                    message);

                _logger.LogError(ex, "Erro ao persistir telemetria. Topic: {Topic}", topic);

                return failureType == TelemetryIngestionFailureType.TransientError
                    ? TelemetryIngestionProcessingResult.TransientFailure(reason)
                    : TelemetryIngestionProcessingResult.DatabaseError(reason);
            }
        }

        const string transientReason = "Falha transitoria apos tentativas de persistencia.";

        await PersistRejectionAsync(
            receivedAtUtc,
            topic,
            payloadJson,
            TelemetryIngestionFailureType.TransientError,
            transientReason,
            cancellationToken,
            topicContext,
            message);

        return TelemetryIngestionProcessingResult.TransientFailure(transientReason);
    }

    private async Task UpdateDeviceSnapshotBestEffortAsync(
        Device provisionedDevice,
        string payloadJson,
        DateTime receivedAtUtc,
        DateTime occurredAtUtc,
        long? sequence,
        CancellationToken cancellationToken)
    {
        try
        {
            provisionedDevice.RegisterTelemetrySnapshot(payloadJson, receivedAtUtc, occurredAtUtc, sequence);
            await _deviceRepository.UpdateAsync(provisionedDevice, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Falha ao atualizar status operacional do device {DeviceId} ({TenantId}/{WorkspaceId}/{Identifier}).",
                provisionedDevice.Id,
                provisionedDevice.TenantId,
                provisionedDevice.WorkspaceId,
                provisionedDevice.Identifier);
        }
    }

    private async Task PersistRejectionAsync(
        DateTime receivedAtUtc,
        string topic,
        string payloadJson,
        TelemetryIngestionFailureType failureType,
        string reason,
        CancellationToken cancellationToken,
        IngestionTopicContext? topicContext = null,
        IncomingTelemetryMessage? message = null)
    {
        try
        {
            var rejection = new TelemetryIngestionRejectionRecord(
                receivedAtUtc,
                topic,
                payloadJson,
                failureType,
                reason,
                message?.TenantId ?? topicContext?.TenantId,
                ParseNullableWorkspace(message?.WorkspaceId) ?? topicContext?.WorkspaceId,
                message?.DeviceId ?? topicContext?.DeviceId,
                message?.MessageType ?? topicContext?.MessageType,
                message?.Sequence);

            await _rejectionRepository.AddAsync(rejection, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha ao registrar rejeicao de ingestao. Topic: {Topic}", topic);
        }
    }

    private static Guid? ParseNullableWorkspace(string? workspaceId)
    {
        return Guid.TryParse(workspaceId, out var value) && value != Guid.Empty
            ? value
            : null;
    }

    private static string? RequireText(
        string? value,
        string fieldName,
        List<string> rejectionReasons)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            rejectionReasons.Add($"{fieldName} ausente.");
            return null;
        }

        return value.Trim();
    }

    private static bool TryParseWorkspaceId(string? value, out Guid workspaceId)
    {
        if (Guid.TryParse(value, out workspaceId) && workspaceId != Guid.Empty)
            return true;

        workspaceId = Guid.Empty;
        return false;
    }

    private static bool TryParseOccurredAtUtc(string? value, out DateTime occurredAtUtc)
    {
        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            occurredAtUtc = timestamp.UtcDateTime;
            return true;
        }

        occurredAtUtc = default;
        return false;
    }

    private static void ValidateTopicPayloadConsistency(
        IngestionTopicContext topicContext,
        string? tenantId,
        Guid workspaceId,
        string? deviceId,
        string? messageType,
        List<string> rejectionReasons)
    {
        if (!string.Equals(topicContext.TenantId, tenantId, StringComparison.Ordinal))
            rejectionReasons.Add("tenantId do payload difere do topic.");

        if (workspaceId != Guid.Empty && topicContext.WorkspaceId != workspaceId)
            rejectionReasons.Add("workspaceId do payload difere do topic.");

        if (!string.Equals(topicContext.DeviceId, deviceId, StringComparison.Ordinal))
            rejectionReasons.Add("deviceId do payload difere do topic.");

        if (!string.Equals(topicContext.MessageType, messageType, StringComparison.OrdinalIgnoreCase))
            rejectionReasons.Add("messageType do payload difere do sufixo do topic.");
    }

    private static bool IsTransient(Exception exception)
    {
        return exception switch
        {
            TimeoutException => true,
            OperationCanceledException => false,
            PostgresException postgres => postgres.IsTransient,
            NpgsqlException npgsql => npgsql.IsTransient,
            DbUpdateException dbUpdate when dbUpdate.InnerException is not null =>
                IsTransient(dbUpdate.InnerException),
            _ when exception.InnerException is not null => IsTransient(exception.InnerException),
            _ => false
        };
    }
}
