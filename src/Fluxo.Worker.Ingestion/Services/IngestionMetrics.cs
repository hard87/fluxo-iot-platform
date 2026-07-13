using System.Diagnostics.Metrics;
using Fluxo.Worker.Ingestion.Models;

namespace Fluxo.Worker.Ingestion.Services;

public interface IIngestionMetrics
{
    void MessageReceived();
    void MessageEnqueued(long currentBufferSize);
    void MessageDequeued(long currentBufferSize);
    void RecordResult(TelemetryIngestionProcessingResult result);
    void MqttReconnected();
    void RecordProcessingDuration(TimeSpan elapsed);
    void RejectionReprocessAttempted();
    void RejectionReprocessResolved();
    void MetricCardinalityGuardTriggered();
}

public sealed class IngestionMetrics : IIngestionMetrics, IDisposable
{
    private readonly ILogger<IngestionMetrics>? _logger;
    private readonly Meter _meter;
    private readonly Counter<long> _receivedCounter;
    private readonly Counter<long> _enqueuedCounter;
    private readonly Counter<long> _dequeuedCounter;
    private readonly Counter<long> _persistedCounter;
    private readonly Counter<long> _rejectedCounter;
    private readonly Counter<long> _duplicateCounter;
    private readonly Counter<long> _databaseFailureCounter;
    private readonly Counter<long> _transientFailureCounter;
    private readonly Counter<long> _processingFailureCounter;
    private readonly Counter<long> _mqttReconnectCounter;
    private readonly Counter<long> _rejectionReprocessAttemptCounter;
    private readonly Counter<long> _rejectionReprocessResolvedCounter;
    private readonly Histogram<double> _processingDurationMs;
    private readonly Counter<long> _metricCardinalityGuardCounter;
    private long _bufferSize;
    private long _maxBufferSize;
    private long _completed;
    private readonly object _durationLock = new();
    private readonly List<double> _durations = [];

    public IngestionMetrics(ILogger<IngestionMetrics>? logger = null)
    {
        _logger = logger;
        _meter = new Meter("Fluxo.Worker.Ingestion", "1.0.0");
        _receivedCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_received");
        _enqueuedCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_enqueued");
        _dequeuedCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_dequeued");
        _persistedCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_persisted");
        _rejectedCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_rejected");
        _duplicateCounter = _meter.CreateCounter<long>("fluxo_ingestion_messages_duplicate");
        _databaseFailureCounter = _meter.CreateCounter<long>("fluxo_ingestion_failures_database");
        _transientFailureCounter = _meter.CreateCounter<long>("fluxo_ingestion_failures_transient");
        _processingFailureCounter = _meter.CreateCounter<long>("fluxo_ingestion_failures_processing");
        _mqttReconnectCounter = _meter.CreateCounter<long>("fluxo_ingestion_mqtt_reconnections");
        _rejectionReprocessAttemptCounter = _meter.CreateCounter<long>("fluxo_ingestion_rejection_reprocess_attempts");
        _rejectionReprocessResolvedCounter = _meter.CreateCounter<long>("fluxo_ingestion_rejection_reprocess_resolved");
        _processingDurationMs = _meter.CreateHistogram<double>("fluxo_ingestion_processing_duration_ms");
        _metricCardinalityGuardCounter = _meter.CreateCounter<long>("metric_cardinality_guard_triggered_total");

        _meter.CreateObservableGauge(
            "fluxo_ingestion_buffer_size",
            () => new Measurement<long>(Volatile.Read(ref _bufferSize)));
    }

    public void MessageReceived() => _receivedCounter.Add(1);

    public void MessageEnqueued(long currentBufferSize)
    {
        _enqueuedCounter.Add(1);
        Interlocked.Exchange(ref _bufferSize, currentBufferSize);
        InterlockedExtensions.Max(ref _maxBufferSize, currentBufferSize);
    }

    public void MessageDequeued(long currentBufferSize)
    {
        _dequeuedCounter.Add(1);
        Interlocked.Exchange(ref _bufferSize, currentBufferSize);
        if (currentBufferSize == 0) LogBenchmarkSnapshot("drained");
    }

    public void RecordResult(TelemetryIngestionProcessingResult result)
    {
        switch (result.Status)
        {
            case TelemetryIngestionProcessingStatus.Persisted:
                _persistedCounter.Add(1);
                break;
            case TelemetryIngestionProcessingStatus.Rejected:
                _rejectedCounter.Add(1);
                break;
            case TelemetryIngestionProcessingStatus.Duplicate:
                _duplicateCounter.Add(1);
                break;
            case TelemetryIngestionProcessingStatus.DatabaseError:
                _databaseFailureCounter.Add(1);
                break;
            case TelemetryIngestionProcessingStatus.TransientFailure:
                _transientFailureCounter.Add(1);
                break;
            case TelemetryIngestionProcessingStatus.ProcessingError:
                _processingFailureCounter.Add(1);
                break;
        }
        if (Interlocked.Increment(ref _completed) % 1000 == 0) LogBenchmarkSnapshot("progress");
    }

    public void MqttReconnected() => _mqttReconnectCounter.Add(1);

    public void RejectionReprocessAttempted() => _rejectionReprocessAttemptCounter.Add(1);

    public void RejectionReprocessResolved() => _rejectionReprocessResolvedCounter.Add(1);
    public void MetricCardinalityGuardTriggered() => _metricCardinalityGuardCounter.Add(1);

    public void RecordProcessingDuration(TimeSpan elapsed)
    {
        var value = Math.Max(elapsed.TotalMilliseconds, 0d); _processingDurationMs.Record(value);
        lock (_durationLock) _durations.Add(value);
    }

    private void LogBenchmarkSnapshot(string reason)
    {
        if (_logger is null) return;
        double[] values; lock (_durationLock) values = [.. _durations.Order()];
        if (values.Length == 0) return;
        double P(double percentile) => values[Math.Min((int)Math.Ceiling(percentile * values.Length) - 1, values.Length - 1)];
        _logger.LogInformation("BENCHMARK_METRICS reason={Reason} completed={Completed} p50_ms={P50:F3} p95_ms={P95:F3} p99_ms={P99:F3} buffer={Buffer} max_buffer={MaxBuffer}",
            reason, Volatile.Read(ref _completed), P(.50), P(.95), P(.99), Volatile.Read(ref _bufferSize), Volatile.Read(ref _maxBufferSize));
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}

internal static class InterlockedExtensions
{
    public static void Max(ref long target, long value)
    { long current; while (value > (current = Volatile.Read(ref target)) && Interlocked.CompareExchange(ref target, value, current) != current) { } }
}
