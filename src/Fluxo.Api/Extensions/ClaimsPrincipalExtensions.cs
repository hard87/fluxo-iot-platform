using System.Security.Claims;
using Fluxo.Application.Common.Exceptions;

namespace Fluxo.Api.Extensions;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var claimValue = principal.FindFirstValue(ClaimTypes.NameIdentifier) ??
                         principal.FindFirstValue("sub");

        if (!Guid.TryParse(claimValue, out var userId) || userId == Guid.Empty)
            throw new UnauthorizedException("User is not authenticated.");

        return userId;
    }
}
