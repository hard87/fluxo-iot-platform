using Fluxo.Api.Extensions;
using Fluxo.Application.Alerts;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Fluxo.Api.Controllers;

[ApiController, Authorize, Route("api/workspaces/{workspaceId:guid}/alerts")]
public sealed class AlertsController(IAlertManagement alerts) : ControllerBase
{
    [HttpPost("rules")]
    public async Task<IActionResult> Create(Guid workspaceId, SaveAlertRuleRequest request, CancellationToken ct) =>
        Ok(await alerts.SaveAsync(User.GetRequiredUserId(), workspaceId, null, request, ct));
    [HttpPut("rules/{ruleId:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, Guid ruleId, SaveAlertRuleRequest request, CancellationToken ct) =>
        Ok(await alerts.SaveAsync(User.GetRequiredUserId(), workspaceId, ruleId, request, ct));
    [HttpGet("rules")]
    public async Task<IActionResult> Rules(Guid workspaceId, CancellationToken ct, int page = 1, string status = "current") =>
        Ok(await alerts.RulesAsync(User.GetRequiredUserId(), workspaceId, page, ct, status));
    [HttpPost("rules/{ruleId:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid ruleId, ArchiveAlertRuleRequest request, CancellationToken ct) =>
        Ok(await alerts.ArchiveAsync(User.GetRequiredUserId(), workspaceId, ruleId, request, ct));
    [HttpGet("rules/{ruleId:guid}/revisions")]
    public async Task<IActionResult> Revisions(Guid workspaceId, Guid ruleId, CancellationToken ct, int page = 1) =>
        Ok(await alerts.RevisionsAsync(User.GetRequiredUserId(), workspaceId, ruleId, page, ct));
    [HttpGet("events")]
    public async Task<IActionResult> Events(Guid workspaceId, CancellationToken ct, int page = 1) =>
        Ok(await alerts.EventsAsync(User.GetRequiredUserId(), workspaceId, page, ct));
    [HttpGet("events/{eventId:guid}")]
    public async Task<IActionResult> History(Guid workspaceId, Guid eventId, CancellationToken ct) =>
        Ok(await alerts.HistoryAsync(User.GetRequiredUserId(), workspaceId, eventId, ct));
    [HttpPost("events/{eventId:guid}/acknowledgements")]
    public async Task<IActionResult> Acknowledge(Guid workspaceId, Guid eventId, CancellationToken ct) =>
        Ok(await alerts.AcknowledgeAsync(User.GetRequiredUserId(), workspaceId, eventId, ct));
    [HttpGet("diagnostics")]
    public async Task<IActionResult> Diagnostics(Guid workspaceId, CancellationToken ct, int page = 1) =>
        Ok(await alerts.DiagnosticsAsync(User.GetRequiredUserId(), workspaceId, page, ct));
    [HttpGet("notifications")]
    public async Task<IActionResult> Notifications(Guid workspaceId, CancellationToken ct, int page = 1) =>
        Ok(await alerts.NotificationsAsync(User.GetRequiredUserId(), workspaceId, page, ct));
    [HttpGet("rules/{ruleId:guid}/portal-recipients")]
    public async Task<IActionResult> PortalRecipients(Guid workspaceId, Guid ruleId, CancellationToken ct) =>
        Ok(await alerts.PortalRecipientsAsync(User.GetRequiredUserId(), workspaceId, ruleId, ct));
    [HttpPost("notifications/{notificationId:guid}/read")]
    public async Task<IActionResult> MarkNotificationRead(Guid workspaceId, Guid notificationId, CancellationToken ct)
    {
        await alerts.MarkNotificationReadAsync(User.GetRequiredUserId(), workspaceId, notificationId, ct);
        return NoContent();
    }
}
