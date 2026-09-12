using Fluxo.Infrastructure.DependencyInjection;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Fluxo.Worker.Ingestion.Workers;
using Fluxo.Infrastructure.Telemetry;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MqttIngestionOptions>(
    builder.Configuration.GetSection(MqttIngestionOptions.SectionName));
builder.Services.Configure<RejectionReprocessingOptions>(
    builder.Configuration.GetSection(RejectionReprocessingOptions.SectionName));

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddScoped<ITelemetryIngestionProcessor, TelemetryIngestionProcessor>();
builder.Services.AddSingleton<IIngestionMetrics, IngestionMetrics>();
builder.Services.AddHostedService<MqttTelemetryIngestionWorker>();
builder.Services.AddHostedService<RejectionReprocessingWorker>();
builder.Services.AddHostedService<AlertEvaluationWorker>();
builder.Services.AddSingleton<TelemetryPartitionState>();
builder.Services.AddHostedService<TelemetryPartitionMaintenanceService>();
builder.Services.AddHealthChecks().AddCheck<TelemetryPartitionHealthCheck>("telemetry_partitions");

var host = builder.Build();
host.Run();
