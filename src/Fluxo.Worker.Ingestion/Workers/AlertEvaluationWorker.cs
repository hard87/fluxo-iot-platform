using Fluxo.Application.Alerts;
using Fluxo.Infrastructure.Alerts;
using Microsoft.Extensions.Options;

namespace Fluxo.Worker.Ingestion.Workers;

public sealed class AlertEvaluationWorker(IServiceScopeFactory scopes, IOptions<AlertEvaluationOptions> options,
    ILogger<AlertEvaluationWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.Enabled) return;
        var workerId = $"alerts-{Guid.NewGuid():N}";
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<AlertEvaluationEngine>();
                var claim = await engine.ClaimAsync(workerId, stoppingToken);
                if (claim is null) { await Task.Delay(options.Value.PollIntervalMilliseconds, stoppingToken); continue; }
                try { await engine.EvaluateAsync(claim, stoppingToken); }
                catch (LostAlertLeaseException) { }
                catch (Exception) when (!stoppingToken.IsCancellationRequested)
                {
                    logger.LogWarning("Alert evaluation failed. Workspace {WorkspaceId}, attempt {AttemptId}.", claim.WorkspaceId, claim.AttemptId);
                    try { await engine.FailAsync(claim, stoppingToken); }
                    catch (LostAlertLeaseException) { }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogWarning("Alert worker cycle failed; outstanding leases remain recoverable.");
                await Task.Delay(options.Value.PollIntervalMilliseconds, stoppingToken);
            }
        }
    }
}
