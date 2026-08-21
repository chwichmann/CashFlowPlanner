using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json.Tests;

/// <summary>
/// The exported file stays indented so it is readable and diffable; the browser working copy is
/// written compact because indentation is roughly a quarter of the localStorage quota it consumes.
/// </summary>
public sealed class CompactSerializationTests
{
    [Fact]
    public void CompactOutput_Should_CarryTheSameDataAsIndentedOutput()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var document = plan.ToDocument();

        // Act
        var indented = serializer.SerializeDocument(document);
        var compact = serializer.SerializeDocumentCompact(document);

        // Assert
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(indented), JsonNode.Parse(compact)));
    }

    [Fact]
    public void CompactOutput_Should_BeMateriallySmaller()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var document = plan.ToDocument();

        // Act
        var indented = serializer.SerializeDocument(document);
        var compact = serializer.SerializeDocumentCompact(document);

        // Assert - the measured saving is 25-30%; 15% is a deliberately loose floor.
        Assert.True(
            compact.Length < indented.Length * 0.85,
            $"Compact output was {compact.Length} bytes against {indented.Length} indented.");

        Assert.DoesNotContain("\n", compact);
    }

    [Fact]
    public void CompactOutput_Should_RoundtripThroughTheNormalReader()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var compact = serializer.SerializeDocumentCompact(plan.ToDocument());
        var reloaded = serializer.DeserializePlan(compact);

        // Assert
        Assert.Equal(plan.Id, reloaded.Id);
        Assert.Equal(plan.Accounts.Count, reloaded.Accounts.Count);
        Assert.Equal(plan.Transactions.Count, reloaded.Transactions.Count);
    }

    [Fact]
    public void CompactOutput_Should_ValidateLikeTheIndentedWriter()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var document = plan.ToDocument();
        document.SchemaVersion = 7;

        // Act - the write path stamps the current version, exactly like SerializeDocument.
        var compact = serializer.SerializeDocumentCompact(document);

        // Assert
        Assert.Equal(
            CashFlowPlanJsonSerializer.CurrentSchemaVersion,
            (int?)JsonNode.Parse(compact)!["schemaVersion"]);
    }
}
