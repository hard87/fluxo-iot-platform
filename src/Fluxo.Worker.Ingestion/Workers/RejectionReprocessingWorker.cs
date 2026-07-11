using Fluxo.Application.Interfaces.Repositories;
using Fluxo.Worker.Ingestion.Models;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Microsoft.Extensions.Options;

namespace Fluxo.Worker.Ingestion.Workers;

/// Periodically replays "root" ingestion rejections (transient/database failures at time of
/// ingestion) through the same TelemetryIngestionProcessor used for live messages. Each
/// attempt's outcome is recorded on the original rejection row and, if the retry itself fails,
/// a new rejection row is persisted too (stamped with SourceRejectionId) purely as an audit
/// trail — it is not itself independently eligible for reprocessing, so retries can't fan out.
public class RejectionReprocessingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<RejectionReprocessingWorker> _logger;
    private readonly RejectionReprocessingOptions _options;

    public RejectionReprocessingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<RejectionReprocessingOptions> options,
        ILogger<RejectionReprocessingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = options.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("RejectionReprocessingWorker desabilitado (RejectionReprocessing:Enabled=false).");
            return;
        }

        var interval = TimeSpan.FromSeconds(Math.Max(_options.IntervalSeconds, 10));

        _logger.LogInformation(
            "RejectionReprocessingWorker iniciado. Intervalo: {Interval}s. BatchSize: {BatchSize}. MaxAttempts: {MaxAttempts}. ErrorTypes: {ErrorTypes}.",
            interval.TotalSeconds,
            _options.BatchSize,
            _options.MaxAttempts,
            string.Join(",", _options.ErrorTypes));

        using var timer = new PeriodicTimer(interval);

        do
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha no ciclo de reprocessamento de rejeicoes.");
            }
        }
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();
        var rejectionRepository = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionRejectionRepository>();
        var processor = scope.ServiceProvider.GetRequiredService<ITelemetryIngestionProcessor>();
        var metrics = scope.ServiceProvider.GetRequiredService<IIngestionMetrics>();

        var batch = await rejectionRepository.GetReprocessableBatchAsync(
            _options.ErrorTypes,
            _options.MaxAttempts,
            _options.BatchSize,
            cancellationToken);

        if (batch.Count == 0)
            return;

        _logger.LogInformation("Reprocessando {Count} rejeicoes elegiveis.", batch.Count);

        var resolvedCount = 0;

        foreach (var rejection in batch)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            var attemptedAtUtc = DateTime.UtcNow;
            metrics.RejectionReprocessAttempted();

            var result = await processor.ProcessAsync(
                rejection.Topic,
                rejection.PayloadRaw,
                rejection.ReceivedAtUtc,
                cancellationToken,
                sourceRejectionId: rejection.Id);

            metrics.RecordResult(result);

            var resolved = result.Status is TelemetryIngestionProcessingStatus.Persisted
                or TelemetryIngestionProcessingStatus.Duplicate;

            if (resolved)
            {
                resolvedCount++;
                metrics.RejectionReprocessResolved();
            }

            await rejectionRepository.RecordReprocessAttemptAsync(
                rejection.Id,
                attemptedAtUtc,
                resolved,
                cancellationToken);
        }

        _logger.LogInformation(
            "Ciclo de reprocessamento concluido. {Resolved}/{Total} rejeicoes resolvidas.",
            resolvedCount,
            batch.Count);
    }
}
