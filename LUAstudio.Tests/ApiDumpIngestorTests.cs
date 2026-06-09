using LUAstudio.IntelliSense.Roblox;
using Xunit;

namespace LUAstudio.Tests;

public sealed class ApiDumpIngestorTests
{
    [Fact]
    public void BuiltIn_fallback_has_DataModel_GetService()
    {
        var result = ApiDumpIngestor.BuildBuiltInFallback();
        Assert.True(result.Classes.ContainsKey("DataModel"));
        Assert.Contains(result.Classes["DataModel"].Members, m => m.Name == "GetService");
    }

    [Fact]
    public void Ingest_json_classes()
    {
        const string json = """
            { "Classes": [ { "Name": "Foo", "Members": [ { "Name": "Bar", "MemberType": "Property" } ] } ] }
            """;
        var result = ApiDumpIngestor.Ingest(json);
        Assert.True(result.Classes.ContainsKey("Foo"));
    }
}
