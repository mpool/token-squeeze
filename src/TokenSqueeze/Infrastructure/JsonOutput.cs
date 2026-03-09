using System.Text.Json;

namespace TokenSqueeze.Infrastructure;

internal static class JsonOutput
{
    public static void Write<T>(T value)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(value, JsonDefaults.Options));
    }

    public static void WriteError(string message, int code = 1)
    {
        Console.Out.WriteLine(JsonSerializer.Serialize(new
        {
            error = message,
            code
        }, JsonDefaults.Options));
    }
}
