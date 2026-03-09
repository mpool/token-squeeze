using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenSqueeze.Infrastructure;

internal static class JsonDefaults
{
    public static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}
