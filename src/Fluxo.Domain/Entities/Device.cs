using System.Text.Json;
using Fluxo.Domain.Enums;

namespace Fluxo.Domain.Entities;

public sealed class Device
{
    private Device()
    {
    }

    public Guid Id { get; private set; }
    public Guid WorkspaceId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Identifier { get; private set; } = string.Empty;
    public DeviceCategory Category { get; private set; }
    public string? MetadataJson { get; private set; }
    public bool IsActive { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public Device(
        Guid workspaceId,
        string name,
        string identifier,
        DeviceCategory category,
        string? metadataJson = null)
    {
        if (workspaceId == Guid.Empty)
            throw new ArgumentException("WorkspaceId is required.", nameof(workspaceId));

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name is required.", nameof(name));

        if (string.IsNullOrWhiteSpace(identifier))
            throw new ArgumentException("Identifier is required.", nameof(identifier));

        if (!Enum.IsDefined(category))
            throw new ArgumentException("Category is invalid.", nameof(category));

        ValidateJsonIfProvided(metadataJson, nameof(metadataJson));

        Id = Guid.NewGuid();
        WorkspaceId = workspaceId;
        Name = name.Trim();
        Identifier = identifier.Trim();
        Category = category;
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
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

    public void UpdateMetadata(string? metadataJson)
    {
        ValidateJsonIfProvided(metadataJson, nameof(metadataJson));
        MetadataJson = string.IsNullOrWhiteSpace(metadataJson) ? null : metadataJson.Trim();
    }

    private static void ValidateJsonIfProvided(string? json, string paramName)
    {
        if (string.IsNullOrWhiteSpace(json))
            return;

        try
        {
            _ = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new ArgumentException("MetadataJson must be a valid JSON document.", paramName, ex);
        }
    }
}
