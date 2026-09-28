using System.Text.Json;
using System.Text.Json.Serialization;

namespace DdoItemTracker.Core.Persistence;

public static class TrackerJson
{
    public static JsonSerializerOptions Options { get; } = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };
}
