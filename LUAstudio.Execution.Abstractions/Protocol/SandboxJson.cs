using System.Text.Json;
using System.Text.Json.Serialization;
using LUAstudio.Execution.Abstractions;

namespace LUAstudio.Execution.Abstractions.Protocol;

public static class SandboxJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public static string Serialize(SandboxEnvelope envelope) => JsonSerializer.Serialize(envelope, Options);

    public static SandboxEnvelope? Deserialize(string json) => JsonSerializer.Deserialize<SandboxEnvelope>(json, Options);
}

public static class SandboxPayload
{
    public static T? As<T>(object? payload)
    {
        if (payload is null)
        {
            return default;
        }

        if (payload is JsonElement element)
        {
            return element.Deserialize<T>(SandboxJson.Options);
        }

        if (payload is T typed)
        {
            return typed;
        }

        var json = JsonSerializer.Serialize(payload, SandboxJson.Options);
        return JsonSerializer.Deserialize<T>(json, SandboxJson.Options);
    }
}

public static class SandboxPipeNames
{
    public const string DefaultHostPipe = "LUAstudio.ExecutionHost";

    public static string ForIdeProcess(int processId)
    {
        if (processId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(processId));
        }

        return $"LUAstudio-{processId}";
    }
}
