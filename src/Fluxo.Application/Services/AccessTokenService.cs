namespace Fluxo.Application.Services;

public sealed record AccessTokenPayload(
    Guid UserId,
    string Email,
    string[] WorkspaceIds);

public sealed record AccessTokenIssueResult(
    string AccessToken,
    DateTime ExpiresAtUtc);

public interface IAccessTokenService
{
    AccessTokenIssueResult Issue(AccessTokenPayload payload);
}
