using Fluxo.Api.Extensions;
using Fluxo.Application.DTOs.Portal;
using Fluxo.Application.UseCases.Portal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/telemetry-rejections")]
public sealed class TelemetryRejectionsController(ListWorkspaceTelemetryRejectionsUseCase list) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<TelemetryRejectionPageResponse>> Get(
        Guid workspaceId, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        Ok(await list.ExecuteAsync(User.GetRequiredUserId(), workspaceId, page, pageSize, search, cancellationToken));
}
