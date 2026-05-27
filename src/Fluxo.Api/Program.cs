using Fluxo.Api.Middleware;
using Fluxo.Api.Configuration;
using Fluxo.Api.Services;
using Fluxo.Application.Options;
using Fluxo.Application.Services;
using Fluxo.Application.UseCases.Auth;
using Fluxo.Application.UseCases.Devices;
using Fluxo.Application.UseCases.Portal;
using Fluxo.Application.UseCases.Provisioning;
using Fluxo.Application.UseCases.Telemetry;
using Fluxo.Application.UseCases.Workspaces;
using Fluxo.Infrastructure.Data;
using Fluxo.Infrastructure.DependencyInjection;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddOpenApi();
builder.Services.AddCors(options =>
{
    var allowedOrigins = builder.Configuration
        .GetSection("Cors:AllowedOrigins")
        .Get<string[]>() ?? ["http://localhost:5173", "http://localhost:8080"];

    options.AddPolicy("Portal", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
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
builder.Services.AddScoped<RegisterPlatformUserUseCase>();
builder.Services.AddScoped<LoginPlatformUserUseCase>();
builder.Services.AddScoped<GetAuthenticatedUserUseCase>();
builder.Services.AddScoped<CreateWorkspaceUseCase>();
builder.Services.AddScoped<ListUserWorkspacesUseCase>();
builder.Services.AddScoped<GetAuthorizedWorkspaceUseCase>();
builder.Services.AddScoped<CreateWorkspaceDeviceUseCase>();
builder.Services.AddScoped<ListWorkspaceDevicesUseCase>();
builder.Services.AddScoped<GetWorkspaceDeviceUseCase>();
builder.Services.AddScoped<ProvisionWorkspaceDeviceUseCase>();
builder.Services.AddScoped<GetWorkspaceDeviceProvisioningUseCase>();
builder.Services.AddScoped<RotateWorkspaceDeviceCredentialUseCase>();
builder.Services.AddScoped<GetWorkspaceDeviceTelemetryUseCase>();
builder.Services.AddScoped<GetWorkspaceDashboardUseCase>();
builder.Services.AddSingleton<IDeviceCredentialSecretService, DeviceCredentialSecretService>();
builder.Services.AddSingleton<IUserPasswordService, UserPasswordService>();
builder.Services.AddSingleton<IAccessTokenService, JwtAccessTokenService>();

var app = builder.Build();

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseCors("Portal");
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
