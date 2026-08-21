using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json;

/// <summary>
/// One step in the ordered upgrade chain applied to a plan document before it is deserialized.
/// Migrations run on the raw JSON tree, not on the mapped document, because "the field was absent"
/// and "the field was present with the default value" are indistinguishable after deserialization.
/// </summary>
public interface ICashFlowPlanDocumentMigration
{
    /// <summary>
    /// The migration runs for documents whose schema version is at most this value.
    /// </summary>
    int AppliesToSchemaVersionUpTo { get; }

    /// <summary>
    /// Short human-readable description, used in diagnostics.
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Applies the migration in place. Must be idempotent: running it twice on the same document
    /// has to produce the same result as running it once.
    /// </summary>
    /// <returns><see langword="true"/> when the document was changed.</returns>
    bool Apply(JsonObject document);
}
