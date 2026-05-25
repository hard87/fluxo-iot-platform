using Fluxo.Api.Middleware;
using Fluxo.Api.Configuration;
using Fluxo.Application.Options;
using Fluxo.Application.Services;
using Fluxo.Application.UseCases.Devices;
using Fluxo.Application.UseCases.Provisioning;
using Fluxo.Application.UseCases.Telemetry;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddFluxoSecurity(builder.Configuration);
builder.Services.Configure<DeviceStatusOptions>(
    builder.Configuration.GetSection(DeviceStatusOptions.SectionName));
builder.Services
    .AddHealthChecks()
    .AddDbContextCheck<FluxoDbContext>("postgresql");

builder.Services.AddScoped<CreateDeviceUseCase>();
builder.Services.AddScoped<GetDeviceByIdUseCase>();
builder.Services.AddScoped<ListDevicesUseCase>();
builder.Services.AddScoped<ProvisionDeviceUseCase>();
builder.Services.AddScoped<GetDeviceProvisioningDetailsUseCase>();
builder.Services.AddScoped<RotateDeviceCredentialUseCase>();
builder.Services.AddScoped<RegisterTelemetryUseCase>();
builder.Services.AddScoped<GetTelemetryByDeviceUseCase>();
builder.Services.AddSingleton<IDeviceCredentialSecretService, DeviceCredentialSecretService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false
});

app.Run();

public partial class Program
{
}
