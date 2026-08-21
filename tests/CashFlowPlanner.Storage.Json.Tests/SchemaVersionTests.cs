using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json.Tests;

/// <summary>
/// Schema versioning behaviour (finding P2b): a newer file must fail with an actionable message,
/// an older or undeclared version must be migrated rather than rejected, and every write must
/// stamp the version this build produces.
/// </summary>
public sealed class SchemaVersionTests
{
    private const string MinimalPlanTemplate = """
    {
      {{VERSION}}
      "planId": "00000000-0000-0000-0000-000000000001",
      "name": "Private Cashflow",
      "baseCurrency": "CHF",
      "persons": [],
      "accounts": [],
      "transactions": [],
      "simulationSettings": {
        "dateMode": "ExplicitDateRange",
        "startDate": "2026-06-01",
        "endDate": "2026-12-31"
      }
    }
    """;

    private static string PlanJson(string? schemaVersionLine)
    {
        return MinimalPlanTemplate.Replace(
            "{{VERSION}}",
            schemaVersionLine ?? string.Empty);
    }

    [Fact]
    public void CurrentSchemaVersion_Should_BeOne()
    {
        Assert.Equal(1, CashFlowPlanJsonSerializer.CurrentSchemaVersion);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(999)]
    public void NewerSchemaVersion_Should_FailWithAnActionableMessage(int schemaVersion)
    {
        // Arrange
        var json = PlanJson($"\"schemaVersion\": {schemaVersion},");
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<NotSupportedException>(
            () => serializer.DeserializeDocument(json));

        // Assert - the message has to name the version, name what this build supports, say what to
        // do about it, and reassure the user that the file is untouched.
        Assert.Contains($"'{schemaVersion}'", exception.Message);
        Assert.Contains("reads up to version 1", exception.Message);
        Assert.Contains("Update CashFlowPlanner", exception.Message);
        Assert.Contains("has not been changed", exception.Message);
    }

    [Fact]
    public void AbsentSchemaVersion_Should_BeAcceptedAndStampedOnWrite()
    {
        // Arrange
        var json = PlanJson(schemaVersionLine: null);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(json);
        var savedJson = serializer.SerializeDocument(document);

        // Assert
        Assert.Equal(CashFlowPlanJsonSerializer.CurrentSchemaVersion, document.SchemaVersion);
        Assert.Equal(
            CashFlowPlanJsonSerializer.CurrentSchemaVersion,
            (int?)JsonNode.Parse(savedJson)!["schemaVersion"]);
    }

    [Fact]
    public void AbsentSchemaVersion_Should_NotLeakIntoTheExtensionDataBucket()
    {
        // Arrange
        var json = PlanJson(schemaVersionLine: null);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(json);

        // Assert
        Assert.True(
            document.ExtensionData is null ||
            !document.ExtensionData.ContainsKey("schemaVersion"));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void InvalidSchemaVersion_Should_BeRejectedAsCorrupt(int schemaVersion)
    {
        // Arrange
        var json = PlanJson($"\"schemaVersion\": {schemaVersion},");
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.DeserializeDocument(json));

        // Assert
        Assert.Contains("not a valid version", exception.Message);
    }

    [Fact]
    public void NonNumericSchemaVersion_Should_BeRejectedAsCorrupt()
    {
        // Arrange
        var json = PlanJson("\"schemaVersion\": \"one\",");
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.DeserializeDocument(json));

        // Assert
        Assert.Contains("non-numeric schema version", exception.Message);
    }

    [Fact]
    public void MalformedJson_Should_FailWithAReadableMessage()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.DeserializeDocument("{ this is not json"));

        // Assert
        Assert.Contains("not valid JSON", exception.Message);
    }

    [Fact]
    public void SerializeDocument_Should_StampTheCurrentSchemaVersion()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var staleDocument = plan.ToDocument();
        staleDocument.SchemaVersion = 0;

        // Act
        var json = serializer.SerializeDocument(staleDocument);

        // Assert
        Assert.Equal(
            CashFlowPlanJsonSerializer.CurrentSchemaVersion,
            (int?)JsonNode.Parse(json)!["schemaVersion"]);
    }

    [Fact]
    public void MigrationChain_Should_BeOrderedOldestFirst()
    {
        // Arrange / Act
        var chain = CashFlowPlanDocumentMigrator.Chain;

        // Assert
        var versions = chain
            .Select(migration => migration.AppliesToSchemaVersionUpTo)
            .ToList();

        Assert.Equal(versions.OrderBy(version => version), versions);

        Assert.All(
            chain,
            migration => Assert.InRange(
                migration.AppliesToSchemaVersionUpTo,
                1,
                CashFlowPlanJsonSerializer.CurrentSchemaVersion));
    }

    [Fact]
    public void EveryMigration_Should_BeIdempotent()
    {
        // Arrange
        var originalJson = File.ReadAllText(
            Path.Combine(AppContext.BaseDirectory, "Samples", "private-cashflow.sample.json"));

        var first = JsonNode.Parse(originalJson)!.AsObject();

        // Act
        var firstRun = CashFlowPlanDocumentMigrator.Migrate(first);
        var secondRun = CashFlowPlanDocumentMigrator.Migrate(first);

        // Assert - re-running the chain over an already migrated document changes nothing.
        Assert.Empty(secondRun);
        Assert.True(
            firstRun.Count >= secondRun.Count,
            "A migration reported work on the second pass that it did not report on the first.");
    }
}
