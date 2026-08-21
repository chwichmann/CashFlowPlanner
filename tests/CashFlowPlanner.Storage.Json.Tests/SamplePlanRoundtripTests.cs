using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json.Tests;

/// <summary>
/// Round-trip guarantees for the file format. The plan file is the source of truth, so loading and
/// saving it must never silently delete anything (finding P2a).
/// </summary>
public sealed class SamplePlanRoundtripTests
{
    private static string ShippedSamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Samples", "private-cashflow.sample.json");

    [Fact]
    public void ShippedSample_Should_RoundtripWithoutFieldLoss()
    {
        // Arrange
        var originalJson = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(originalJson);
        var savedJson = serializer.SerializeDocument(document);

        // Assert
        JsonAssert.ContainsAllOf(
            JsonNode.Parse(originalJson),
            JsonNode.Parse(savedJson));
    }

    [Fact]
    public void ShippedSample_Should_KeepDocumentLevelMetadata()
    {
        // Arrange
        var originalJson = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(originalJson);
        var savedJson = serializer.SerializeDocument(document);

        var saved = JsonNode.Parse(savedJson)!.AsObject();

        // Assert
        Assert.Equal("2026-06-01T00:00:00Z", (string?)saved["createdAt"]);
        Assert.Equal("2026-06-01T00:00:00Z", (string?)saved["modifiedAt"]);
        Assert.Equal("Sample file for first Blazor WASM MVP.", (string?)saved["notes"]);
    }

    [Fact]
    public void UnknownTopLevelFields_Should_SurviveARoundtrip()
    {
        // Arrange
        var originalJson = """
        {
          "schemaVersion": 1,
          "planId": "00000000-0000-0000-0000-000000000001",
          "name": "Private Cashflow",
          "baseCurrency": "CHF",
          "accounts": [],
          "transactions": [],
          "simulationSettings": {
            "dateMode": "ExplicitDateRange",
            "startDate": "2026-06-01",
            "endDate": "2026-12-31"
          },
          "encryptionEnvelope": {
            "algorithm": "AES-GCM",
            "iterations": 600000
          },
          "futureFlag": true,
          "futureList": [1, 2, 3]
        }
        """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(originalJson);
        var savedJson = serializer.SerializeDocument(document);

        var saved = JsonNode.Parse(savedJson)!.AsObject();

        // Assert
        Assert.Equal("AES-GCM", (string?)saved["encryptionEnvelope"]?["algorithm"]);
        Assert.Equal(600000, (int?)saved["encryptionEnvelope"]?["iterations"]);
        Assert.True((bool?)saved["futureFlag"]);
        Assert.Equal(3, saved["futureList"]?.AsArray().Count);
    }

    [Fact]
    public void UnknownFields_Should_SurviveAPlanEditThatRewritesTheDocument()
    {
        // Arrange
        var originalJson = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        var document = serializer.DeserializeDocument(originalJson);
        var plan = document.ToPlan();

        // Act - this is what the app does on every mutation: map the plan back onto the document.
        var savedJson = serializer.SerializePlan(plan, document);
        var saved = JsonNode.Parse(savedJson)!.AsObject();

        // Assert
        Assert.Equal("Sample file for first Blazor WASM MVP.", (string?)saved["notes"]);
        Assert.NotNull(saved["createdAt"]);
    }

    [Fact]
    public void SaveOfALoad_Should_BeIdempotent()
    {
        // Arrange
        var originalJson = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var firstPass = serializer.SerializeDocument(
            serializer.DeserializeDocument(originalJson));

        var secondPass = serializer.SerializeDocument(
            serializer.DeserializeDocument(firstPass));

        var thirdPass = serializer.SerializeDocument(
            serializer.DeserializeDocument(secondPass));

        // Assert
        Assert.Equal(firstPass, secondPass);
        Assert.Equal(secondPass, thirdPass);
    }

    [Fact]
    public void ShippedSample_Should_StillBeLoadableAsAPlan()
    {
        // Arrange
        var originalJson = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var plan = serializer.DeserializePlan(originalJson);

        // Assert
        Assert.Equal("Private Cashflow Sample", plan.Name);
        Assert.Equal(3, plan.Accounts.Count);
        Assert.Equal(4, plan.Transactions.Count);
    }
}
