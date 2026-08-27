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

    [Fact]
    public void Ingest_accepts_object_type_descriptors()
    {
        const string json = """
            {
              "Classes": [
                {
                  "Name": "Foo",
                  "Superclass": "Instance",
                  "Members": [
                    {
                      "Name": "Bar",
                      "MemberType": "Function",
                      "ReturnType": { "Name": "string", "Category": "Primitive" }
                    }
                  ]
                }
              ]
            }
            """;

        var result = ApiDumpIngestor.Ingest(json);

        Assert.Equal("string", Assert.Single(result.Classes["Foo"].Members).ReturnType);
    }
}
