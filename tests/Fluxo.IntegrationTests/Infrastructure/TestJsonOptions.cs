using System.Text.Json;
using System.Text.Json.Serialization;
using Fluxo.Domain.Enums;

namespace Fluxo.IntegrationTests.Infrastructure;

/// <summary>
/// Mirrors the API's DeviceCategory JSON contract (see Fluxo.Api/Program.cs) for tests that
/// deserialize a response containing it. ReadFromJsonAsync without explicit options uses the
/// System.Text.Json defaults, which reject the string the API now sends.
/// </summary>
internal static class TestJsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter<DeviceCategory>() }
    };
}
