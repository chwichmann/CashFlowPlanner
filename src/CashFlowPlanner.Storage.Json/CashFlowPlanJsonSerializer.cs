using CashFlowPlanner.Core;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json;

public sealed class CashFlowPlanJsonSerializer
{
    /// <summary>
    /// The schema version this build writes. Documents at or below this version are accepted and
    /// upgraded through the ordered migration chain; anything above is rejected.
    /// </summary>
    public const int CurrentSchemaVersion = 1;

    private readonly JsonSerializerOptions _jsonOptions;

    public CashFlowPlanJsonSerializer()
        : this(CashFlowPlanJsonOptions.Create())
    {
    }

    public CashFlowPlanJsonSerializer(JsonSerializerOptions jsonOptions)
    {
        _jsonOptions = jsonOptions;
    }

    public string SerializeDocument(CashFlowPlanDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        StampCurrentSchemaVersion(document);
        ValidateDocument(document);

        return JsonSerializer.Serialize(document, _jsonOptions);
    }

    public string SerializePlan(
        CashFlowPlan plan,
        CashFlowPlanDocument? existingDocument = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        plan.Validate();

        var document = plan.ToDocument(existingDocument);

        return SerializeDocument(document);
    }

    public CashFlowPlanDocument DeserializeDocument(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            throw new InvalidOperationException("JSON content is empty.");
        }

        JsonObject? root;

        try
        {
            root = JsonSerializer.Deserialize<JsonObject>(json, _jsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The cashflow plan file is not valid JSON: {exception.Message}",
                exception);
        }

        return MigrateAndBind(root);
    }

    public CashFlowPlan DeserializePlan(string json)
    {
        var document = DeserializeDocument(json);
        var plan = document.ToPlan();

        plan.Validate();

        return plan;
    }

    public async Task<string> SerializeDocumentAsync(
        CashFlowPlanDocument document,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);

        StampCurrentSchemaVersion(document);
        ValidateDocument(document);

        await using var stream = new MemoryStream();

        await JsonSerializer.SerializeAsync(
            stream,
            document,
            _jsonOptions,
            cancellationToken);

        stream.Position = 0;

        using var reader = new StreamReader(stream);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    public async Task<CashFlowPlanDocument> DeserializeDocumentAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);

        JsonObject? root;

        try
        {
            root = await JsonSerializer.DeserializeAsync<JsonObject>(
                stream,
                _jsonOptions,
                cancellationToken);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The cashflow plan file is not valid JSON: {exception.Message}",
                exception);
        }

        return MigrateAndBind(root);
    }

    public async Task<CashFlowPlan> DeserializePlanAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var document = await DeserializeDocumentAsync(stream, cancellationToken);
        var plan = document.ToPlan();

        plan.Validate();

        return plan;
    }

    /// <summary>
    /// Runs the migration chain over the raw JSON tree and binds the result to a document. The
    /// migrations have to see the raw tree: after binding, an absent property and a property that
    /// happens to carry the .NET default are indistinguishable.
    /// </summary>
    private CashFlowPlanDocument MigrateAndBind(JsonObject? root)
    {
        if (root is null)
        {
            throw new InvalidOperationException("Could not deserialize cashflow plan document.");
        }

        CashFlowPlanDocumentMigrator.Migrate(root);

        CashFlowPlanDocument? document;

        try
        {
            document = root.Deserialize<CashFlowPlanDocument>(_jsonOptions);
        }
        catch (JsonException exception)
        {
            throw new InvalidOperationException(
                $"The cashflow plan file could not be read: {exception.Message}",
                exception);
        }

        if (document is null)
        {
            throw new InvalidOperationException("Could not deserialize cashflow plan document.");
        }

        ValidateDocument(document);

        return document;
    }

    private static void StampCurrentSchemaVersion(CashFlowPlanDocument document)
    {
        document.SchemaVersion = CurrentSchemaVersion;
    }

    private static void ValidateDocument(CashFlowPlanDocument document)
    {
        CashFlowPlanDocumentMigrator.EnsureSupported(document.SchemaVersion);

        if (document.PlanId == Guid.Empty)
        {
            throw new InvalidOperationException("PlanId must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(document.Name))
        {
            throw new InvalidOperationException("Document name is required.");
        }

        if (string.IsNullOrWhiteSpace(document.BaseCurrency))
        {
            throw new InvalidOperationException("Document base currency is required.");
        }

        if (document.Accounts is null)
        {
            throw new InvalidOperationException("Document accounts list is required.");
        }

        if (document.Transactions is null)
        {
            throw new InvalidOperationException("Document transactions list is required.");
        }

        var plan = document.ToPlan();
        plan.Validate();
    }
}