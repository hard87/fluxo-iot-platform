using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/status")]
public sealed class StatusController : ControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public StatusController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var health = await _healthCheckService.CheckHealthAsync(cancellationToken);

        var response = new
        {
            status = health.Status.ToString(),
            checkedAtUtc = DateTime.UtcNow,
            components = health.Entries.Select(entry => new
            {
                name = entry.Key,
                status = entry.Value.Status.ToString(),
                durationMs = entry.Value.Duration.TotalMilliseconds,
                description = entry.Value.Description
            })
        };

        var statusCode = health.Status == HealthStatus.Healthy
            ? StatusCodes.Status200OK
            : StatusCodes.Status503ServiceUnavailable;

        return StatusCode(statusCode, response);
    }
}
