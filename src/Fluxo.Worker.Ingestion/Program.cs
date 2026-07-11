using Fluxo.Infrastructure.DependencyInjection;
using Fluxo.Worker.Ingestion.Options;
using Fluxo.Worker.Ingestion.Services;
using Fluxo.Worker.Ingestion.Workers;

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

var host = builder.Build();
host.Run();
