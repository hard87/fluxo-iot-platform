using System.Diagnostics.Metrics;
using Fluxo.Worker.Ingestion.Models;

namespace Fluxo.Worker.Ingestion.Services;

public interface IIngestionMetrics
{
    void MessageReceived();
    void MessageEnqueued();
    void MessageDequeued();
    void RecordResult(TelemetryIngestionProcessingResult result);
}

public sealed class IngestionMetrics : IIngestionMetrics, IDisposable
{
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

    public IngestionMetrics()
    {
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
    }

    public void MessageReceived() => _receivedCounter.Add(1);

    public void MessageEnqueued() => _enqueuedCounter.Add(1);

    public void MessageDequeued() => _dequeuedCounter.Add(1);

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
    }

    public void Dispose()
    {
        _meter.Dispose();
    }
}
