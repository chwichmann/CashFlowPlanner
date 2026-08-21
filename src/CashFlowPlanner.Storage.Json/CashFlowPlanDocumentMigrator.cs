using CashFlowPlanner.Storage.Json.Migrations;
using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json;

/// <summary>
/// Upgrades a raw plan document to <see cref="CashFlowPlanJsonSerializer.CurrentSchemaVersion"/>.
///
/// Before finding P2b the serializer hard-rejected every version except 1 with a bare
/// <see cref="NotSupportedException"/> and no way to ever ship a version 2, while an absent version
/// was silently accepted. Now: an absent version is treated as the oldest known version, versions at
/// or below the current one are migrated through an ordered chain, and a newer version produces an
/// actionable message that tells the user what to do and states that their file was not modified.
/// </summary>
public static class CashFlowPlanDocumentMigrator
{
    /// <summary>
    /// The version assumed for documents that carry no <c>schemaVersion</c> at all. Those files
    /// predate the field, so they are the oldest shape we know.
    /// </summary>
    public const int AssumedSchemaVersionWhenAbsent = 1;

    private const string SchemaVersionPropertyName = "schemaVersion";

    /// <summary>
    /// The migration chain, oldest first. Order is significant.
    /// </summary>
    private static readonly ICashFlowPlanDocumentMigration[] Migrations =
    [
        new ExplicitDateRangeMigration()
    ];

    public static IReadOnlyList<ICashFlowPlanDocumentMigration> Chain => Migrations;

    /// <summary>
    /// Reads the declared schema version, or <see cref="AssumedSchemaVersionWhenAbsent"/> when the
    /// document does not declare one.
    /// </summary>
    public static int ReadSchemaVersion(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var versionNode = FindProperty(document, SchemaVersionPropertyName);

        if (versionNode is null)
        {
            return AssumedSchemaVersionWhenAbsent;
        }

        try
        {
            return versionNode.GetValue<int>();
        }
        catch (Exception exception) when (
            exception is FormatException or InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"The cashflow plan file declares a non-numeric schema version " +
                $"({versionNode.ToJsonString()}). The file is corrupt or is not a cashflow plan.",
                exception);
        }
    }

    /// <summary>
    /// Validates the schema version and applies every migration that targets it, in order. The
    /// document is modified in place and stamped with the current schema version.
    /// </summary>
    /// <returns>The migrations that changed something, in the order they ran.</returns>
    public static IReadOnlyList<string> Migrate(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var version = ReadSchemaVersion(document);

        EnsureSupported(version);

        var applied = new List<string>();

        foreach (var migration in Migrations)
        {
            if (version > migration.AppliesToSchemaVersionUpTo)
            {
                continue;
            }

            if (migration.Apply(document))
            {
                applied.Add(migration.Description);
            }
        }

        SetProperty(
            document,
            SchemaVersionPropertyName,
            JsonValue.Create(CashFlowPlanJsonSerializer.CurrentSchemaVersion));

        return applied;
    }

    /// <summary>
    /// Throws with an actionable message when the document cannot be opened by this build.
    /// </summary>
    public static void EnsureSupported(int schemaVersion)
    {
        if (schemaVersion < 1)
        {
            throw new InvalidOperationException(
                $"The cashflow plan file declares schema version {schemaVersion}, which is not a " +
                "valid version. Schema versions start at 1. The file is corrupt or was not " +
                "produced by CashFlowPlanner.");
        }

        if (schemaVersion > CashFlowPlanJsonSerializer.CurrentSchemaVersion)
        {
            throw new NotSupportedException(
                $"Unsupported cashflow plan schema version '{schemaVersion}'. This build of " +
                $"CashFlowPlanner reads up to version " +
                $"{CashFlowPlanJsonSerializer.CurrentSchemaVersion}, so the file was written by a " +
                "newer build. Update CashFlowPlanner and open the file again - reload the page " +
                "with Ctrl+Shift+R if the browser is still running a cached older build. Your " +
                "file has not been changed.");
        }
    }

    /// <summary>
    /// Case-insensitive property lookup, because the reader accepts case-insensitive JSON.
    /// </summary>
    internal static JsonNode? FindProperty(JsonObject document, string propertyName)
    {
        if (document.TryGetPropertyValue(propertyName, out var node))
        {
            return node;
        }

        foreach (var (key, value) in document)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return value;
            }
        }

        return null;
    }

    internal static bool HasProperty(JsonObject document, string propertyName)
    {
        if (document.ContainsKey(propertyName))
        {
            return true;
        }

        foreach (var (key, _) in document)
        {
            if (string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Sets a property, replacing any existing occurrence regardless of its casing so that a
    /// document written with a different casing does not end up with two competing properties.
    /// </summary>
    internal static void SetProperty(
        JsonObject document,
        string propertyName,
        JsonNode? value)
    {
        var existingKey = document
            .Select(property => property.Key)
            .FirstOrDefault(key =>
                !string.Equals(key, propertyName, StringComparison.Ordinal) &&
                string.Equals(key, propertyName, StringComparison.OrdinalIgnoreCase));

        if (existingKey is not null)
        {
            document.Remove(existingKey);
        }

        document[propertyName] = value;
    }
}
