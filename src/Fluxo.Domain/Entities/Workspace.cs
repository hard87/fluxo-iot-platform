using System.Text.RegularExpressions;

namespace Fluxo.Domain.Entities;

public sealed partial class Workspace
{
    private Workspace()
    {
    }

    public Guid Id { get; private set; }
    public string TenantId { get; private set; } = string.Empty;
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Workspace(string tenantId, string name)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            throw new ArgumentException("TenantId is required.", nameof(tenantId));

        if (!TenantPattern().IsMatch(tenantId.Trim()))
            throw new ArgumentException("TenantId is invalid.", nameof(tenantId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        Id = Guid.NewGuid();
        TenantId = tenantId.Trim().ToLowerInvariant();
        Name = name.Trim();
        IsActive = true;
        CreatedAtUtc = DateTime.UtcNow;
    }

    public void Rename(string newName)
    {
        if (string.IsNullOrWhiteSpace(newName))
            throw new ArgumentException("Name is required.", nameof(newName));

        Name = newName.Trim();
    }

    public void Deactivate()
    {
        IsActive = false;
    }

    [GeneratedRegex("^[a-z0-9\\-]{3,120}$", RegexOptions.Compiled)]
    private static partial Regex TenantPattern();
}
