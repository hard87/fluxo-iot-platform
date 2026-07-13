using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Telemetry;
using Fluxo.Application.UseCases.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}")]
public sealed class TelemetryQueryController(QueryTelemetryUseCase query,
    ListWorkspaceMetricDefinitionsUseCase listMetrics) : ControllerBase
{
    [HttpPost("telemetry/query")]
    public async Task<ActionResult<TelemetryQueryResponse>> Query(Guid workspaceId,
        [FromBody] TelemetryQueryRequest request, CancellationToken cancellationToken) =>
        Ok(await query.ExecuteAsync(User.GetRequiredUserId(), workspaceId, request, HttpContext.RequestAborted));

    [HttpGet("metric-definitions")]
    public async Task<ActionResult<IReadOnlyList<MetricDefinitionResponse>>> ListMetricDefinitions(Guid workspaceId,
        CancellationToken cancellationToken) =>
        Ok(await listMetrics.ExecuteAsync(User.GetRequiredUserId(), workspaceId, HttpContext.RequestAborted));
}
