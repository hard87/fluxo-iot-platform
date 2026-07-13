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
using System.Text;
using System.Text.RegularExpressions;
using Fluxo.Domain.Models;
using Fluxo.Domain.Exceptions;

namespace Fluxo.Worker.Ingestion.Services;

public class TelemetryIngestionProcessor : ITelemetryIngestionProcessor
{
    private static readonly Regex MetricKeyPattern = new("^[a-z][a-z0-9._-]{0,63}$", RegexOptions.Compiled);
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly ITelemetryIngestionRepository _telemetryIngestionRepository;
    private readonly ITelemetryIngestionRejectionRepository _rejectionRepository;
    private readonly IDeviceRepository _deviceRepository;
    private readonly ILogger<TelemetryIngestionProcessor> _logger;
    private readonly MqttIngestionOptions _options;
    private readonly IIngestionMetrics? _metrics;

    public TelemetryIngestionProcessor(
        ITelemetryIngestionRepository telemetryIngestionRepository,
        ITelemetryIngestionRejectionRepository rejectionRepository,
        IDeviceRepository deviceRepository,
        IOptions<MqttIngestionOptions> options,
        ILogger<TelemetryIngestionProcessor> logger,
        IIngestionMetrics? metrics = null)
    {
        _telemetryIngestionRepository = telemetryIngestionRepository;
        _rejectionRepository = rejectionRepository;
        _deviceRepository = deviceRepository;
        _logger = logger;
        _options = options.Value;
        _metrics = metrics;
    }

    public async Task<TelemetryIngestionProcessingResult> ProcessAsync(
        string topic,
        string payloadJson,
        DateTime receivedAtUtc,
        CancellationToken cancellationToken = default,
        Guid? sourceRejectionId = null)
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
                cancellationToken,
                sourceRejectionId: sourceRejectionId);

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
                topicContext,
                sourceRejectionId: sourceRejectionId);

            _logger.LogWarning("Mensagem rejeitada. Payload vazio. Topic: {Topic}", topic);
            return TelemetryIngestionProcessingResult.Rejected(reason);
        }

        if (Encoding.UTF8.GetByteCount(payloadJson) > _options.MaxPayloadBytes)
            return await RejectEarlyAsync(receivedAtUtc, topic, payloadJson, "PayloadTooLarge", topicContext, cancellationToken, sourceRejectionId);

        IncomingTelemetryMessage? message;
        IReadOnlyList<TelemetryMetricValue> canonicalMetrics = Array.Empty<TelemetryMetricValue>();
        var isV2 = false;
        try
        {
            using var document = JsonDocument.Parse(payloadJson);
            isV2 = document.RootElement.TryGetProperty("schemaVersion", out var version) && version.ValueKind == JsonValueKind.Number;
            if (isV2)
            {
                var v2 = JsonSerializer.Deserialize<IncomingTelemetryV2Message>(payloadJson, JsonOptions)!;
                message = new IncomingTelemetryMessage { SchemaVersion = v2.SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    TenantId = topicContext!.TenantId, WorkspaceId = topicContext.WorkspaceId.ToString(), DeviceId = topicContext.DeviceId,
                    MessageType = "telemetry", TimestampUtc = v2.OccurredAtUtc, Sequence = v2.Sequence };
                canonicalMetrics = ParseV2Metrics(v2);
            }
            else message = JsonSerializer.Deserialize<IncomingTelemetryMessage>(payloadJson, JsonOptions);
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
                topicContext,
                sourceRejectionId: sourceRejectionId);

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
                topicContext,
                sourceRejectionId: sourceRejectionId);

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
        { if (!isV2) rejectionReasons.Add("metrics nao pode ser nulo para messageType telemetry."); }

        if (isTelemetryMessage && !message.Sequence.HasValue)
            rejectionReasons.Add("sequence e obrigatorio para messageType telemetry.");
        if (message.Sequence <= 0) rejectionReasons.Add("sequence deve ser positivo.");
        if (isV2 && schemaVersion != "2") rejectionReasons.Add("schemaVersion desconhecida.");
        if (occurredAtUtc != default && (occurredAtUtc > receivedAtUtc.AddMinutes(5) || occurredAtUtc < receivedAtUtc.AddDays(-_options.MaxPastDays)))
            rejectionReasons.Add("TimestampOutsideWindow.");
        if (isV2 && (canonicalMetrics.Count < 1 || canonicalMetrics.Count > 64)) rejectionReasons.Add("metrics deve conter de 1 a 64 valores.");

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
                message,
                sourceRejectionId);

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
                message,
                sourceRejectionId);

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
                message,
                sourceRejectionId);

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

        if (!isV2)
            canonicalMetrics = AdaptLegacyMetrics(message.Metrics!);

        var retryCount = Math.Max(_options.DatabaseRetryCount, 1);
        var retryDelay = TimeSpan.FromMilliseconds(Math.Max(_options.DatabaseRetryDelayMs, 10));

        for (var attempt = 1; attempt <= retryCount; attempt++)
        {
            try
            {
                var writeResult = await _telemetryIngestionRepository.AddWithPointsAsync(record, canonicalMetrics, cancellationToken);

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
                        message,
                        sourceRejectionId);

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
            catch (MetricTypeMismatchException ex)
            {
                await PersistRejectionAsync(receivedAtUtc, topic, payloadJson, TelemetryIngestionFailureType.MetricTypeMismatch, ex.Message,
                    cancellationToken, topicContext, message, sourceRejectionId);
                return TelemetryIngestionProcessingResult.Rejected(ex.Message);
            }
            catch (MetricCardinalityGuardException ex)
            {
                _metrics?.MetricCardinalityGuardTriggered();
                await PersistRejectionAsync(receivedAtUtc, topic, payloadJson, TelemetryIngestionFailureType.MetricCardinalityGuardTriggered, ex.Message,
                    cancellationToken, topicContext, message, sourceRejectionId);
                return TelemetryIngestionProcessingResult.Rejected(ex.Message);
            }
            catch (MetricWorkspaceLimitException ex)
            {
                await PersistRejectionAsync(receivedAtUtc, topic, payloadJson, TelemetryIngestionFailureType.MetricWorkspaceLimitTriggered, ex.Message,
                    cancellationToken, topicContext, message, sourceRejectionId);
                return TelemetryIngestionProcessingResult.Rejected(ex.Message);
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
                    message,
                    sourceRejectionId);

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
            message,
            sourceRejectionId);

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
        IncomingTelemetryMessage? message = null,
        Guid? sourceRejectionId = null)
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
                message?.Sequence,
                sourceRejectionId);

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

    private async Task<TelemetryIngestionProcessingResult> RejectEarlyAsync(DateTime receivedAtUtc, string topic,
        string payload, string reason, IngestionTopicContext? context, CancellationToken cancellationToken, Guid? sourceRejectionId)
    {
        await PersistRejectionAsync(receivedAtUtc, topic, payload, TelemetryIngestionFailureType.Validation, reason,
            cancellationToken, context, sourceRejectionId: sourceRejectionId);
        return TelemetryIngestionProcessingResult.Rejected(reason);
    }

    private static IReadOnlyList<TelemetryMetricValue> ParseV2Metrics(IncomingTelemetryV2Message message)
    {
        if (message.Metrics is null) return [];
        var result = new List<TelemetryMetricValue>(message.Metrics.Count);
        foreach (var (key, value) in message.Metrics)
        {
            if (!MetricKeyPattern.IsMatch(key)) throw new JsonException($"MetricKey invalida: {key}.");
            switch (value.ValueKind)
            {
                case JsonValueKind.Number:
                    if (!value.TryGetDouble(out var number) || !double.IsFinite(number)) throw new JsonException($"Numero invalido: {key}.");
                    result.Add(new(key, MetricValueType.Numeric, NumericValue: number)); break;
                case JsonValueKind.True: case JsonValueKind.False:
                    result.Add(new(key, MetricValueType.Boolean, BooleanValue: value.GetBoolean())); break;
                case JsonValueKind.String:
                    var text = value.GetString()!;
                    if (text.Length > 256) throw new JsonException($"Texto excede 256 caracteres: {key}.");
                    result.Add(new(key, MetricValueType.Text, TextValue: text)); break;
                default: throw new JsonException($"UnsupportedMetricValueType: {key}.");
            }
        }
        return result;
    }

    private static IReadOnlyList<TelemetryMetricValue> AdaptLegacyMetrics(IncomingTelemetryMetrics metrics)
    {
        var result = new List<TelemetryMetricValue>(5);
        if (metrics.Temperature is { } temperature) result.Add(new("temperature", MetricValueType.Numeric, NumericValue: temperature));
        if (metrics.Humidity is { } humidity) result.Add(new("humidity", MetricValueType.Numeric, NumericValue: humidity));
        if (metrics.Battery is { } battery) result.Add(new("battery", MetricValueType.Numeric, NumericValue: battery));
        if (metrics.Rssi is { } rssi) result.Add(new("rssi", MetricValueType.Numeric, NumericValue: rssi));
        if (metrics.UptimeSec is { } uptime) result.Add(new("uptime_sec", MetricValueType.Numeric, NumericValue: uptime));
        return result;
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
