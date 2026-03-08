using System.Text.Json;
using System.Text.Json.Serialization;

namespace TokenSqueeze.Infrastructure;

internal static class JsonOutput
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void Write<T>(T value)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(value, Options));
    }

    public static void WriteError(string message, int code = 1)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            error = message,
            code
        }, Options));
    }
}
