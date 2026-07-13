namespace Fluxo.Domain.Enums;

public static class WorkspaceMembershipRoleExtensions
{
    public static bool SatisfiesMinimum(this WorkspaceMembershipRole role, WorkspaceMembershipRole minimumRole)
    {
        return (int)role <= (int)minimumRole;
    }
}
