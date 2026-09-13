using Fluxo.Domain.Alerts;
using Fluxo.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Fluxo.IntegrationTests.Alerts;

/// <summary>
/// E2.2 of docs/plano-acao-e1-e2-e3-e5.md: regra criada -> telemetria dispara -> evento aparece
/// -> reconhecimento, driven end-to-end through the real <see cref="AlertWorkerHarness"/> (the
/// actual AlertEvaluationWorker BackgroundService), not a manual drain loop.
/// </summary>
public sealed class AlertGoldenPathTests
{
    [SkippableFact]
    public Task Disabled_WorkerNeverEvaluatesAnyRule() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs, enabled: false);
        var rule = await harness.CreateRuleAsync();
        await harness.PublishTelemetryAsync(1, rule.ActivatedAtUtc.AddSeconds(1), 90);

        // Regression guard for the exact bug fixed in fix/enable-alert-evaluation-worker:
        // AlertEvaluation:Enabled shipped unset (default false) in every environment, so the
        // worker's ExecuteAsync returned immediately without ever claiming a work item.
        await Task.Delay(500);

        await using var db = harness.Db();
        Assert.Equal(0, await db.Set<AlertEvent>().CountAsync());
        Assert.Equal(1, await db.Set<AlertEvaluationWorkItem>().CountAsync(x => x.Status == "Pending"));
    });

    [SkippableFact]
    public Task CreateRuleIngestFireHistoryAcknowledgeResolve_ViaRealWorker() => Run(async cs =>
    {
        await using var harness = await AlertWorkerHarness.StartAsync(cs);

        // 1. usuario cria regra no portal (via IAlertManagement, a mesma interface que AlertsController chama)
        var rule = await harness.CreateRuleAsync(harness.Request(durationSeconds: 0));
        var at = rule.ActivatedAtUtc.AddSeconds(1);

        // 2. device publica leituras que satisfazem condicao e duracao
        await harness.PublishTelemetryAsync(1, at, 90);

        // 3. worker real (nao Drain manual) abre um unico evento
        await harness.WaitForAsync(async db => await db.Set<AlertEvent>().CountAsync(x => x.Status == "Firing") == 1);

        Guid occurrenceId;
        await using (var db = harness.Db())
            occurrenceId = (await db.Set<AlertEvent>().SingleAsync()).Id;

        // 4. evento e historico consultaveis pela mesma chamada que a pagina de historico do portal usa
        await using (var read = harness.Db())
        {
            var history = await harness.Management(read).HistoryAsync(harness.User.Id, harness.Workspace.Id, occurrenceId, default);
            Assert.Equal("Firing", history.Event.Status);
            Assert.Single(history.Transitions);
            Assert.Equal("Firing", history.Transitions[0].Kind);

            // 5. intencao de entrega no estado hoje realmente produzido pelo codigo -- nenhum canal
            // (webhook/e-mail) esta implementado ainda, so o rastreio de intencao (ver E2.4).
            Assert.Single(history.DeliveryIntents);
        }

        // 6. usuario reconhece o evento -- reconhecimento duplicado nao cria transicao duplicada
        await using (var db = harness.Db())
        {
            var management = harness.Management(db);
            var ack1 = await management.AcknowledgeAsync(harness.User.Id, harness.Workspace.Id, occurrenceId, default);
            var ack2 = await management.AcknowledgeAsync(harness.User.Id, harness.Workspace.Id, occurrenceId, default);
            Assert.Equal(ack1.Id, ack2.Id);
        }

        // 7. leitura de resolucao fecha o evento sem criar duplicata
        await harness.PublishTelemetryAsync(2, at.AddSeconds(1), 70);
        await harness.WaitForAsync(async db =>
            await db.Set<AlertEvent>().CountAsync() == 1 &&
            (await db.Set<AlertEvent>().SingleAsync()).Status == "Resolved");

        await using var final = harness.Db();
        var closed = await harness.Management(final).HistoryAsync(harness.User.Id, harness.Workspace.Id, occurrenceId, default);
        Assert.Equal("Resolved", closed.Event.Status);
        Assert.Equal(new[] { "Firing", "Resolved" }, closed.Transitions.Select(x => x.Kind));
        Assert.Single(closed.Acknowledgements);
        Assert.Equal(1, await final.Set<AlertEvent>().CountAsync());
    });

    private static Task Run(Func<string, Task> test)
    {
        DisposableTestDatabase.SkipUnlessAvailable();
        return DisposableTestDatabase.WithDatabaseAsync("it", test);
    }
}
