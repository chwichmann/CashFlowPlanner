using CashFlowPlanner.Core;
using System.Text.Json;

namespace CashFlowPlanner.Storage.Json;

public sealed class CashFlowPlanJsonSerializer
{
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

        var document = JsonSerializer.Deserialize<CashFlowPlanDocument>(
            json,
            _jsonOptions);

        if (document is null)
        {
            throw new InvalidOperationException("Could not deserialize cashflow plan document.");
        }

        ValidateDocument(document);

        return document;
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

        var document = await JsonSerializer.DeserializeAsync<CashFlowPlanDocument>(
            stream,
            _jsonOptions,
            cancellationToken);

        if (document is null)
        {
            throw new InvalidOperationException("Could not deserialize cashflow plan document.");
        }

        ValidateDocument(document);

        return document;
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

    private static void ValidateDocument(CashFlowPlanDocument document)
    {
        if (document.SchemaVersion != 1)
        {
            throw new NotSupportedException(
                $"Unsupported cashflow plan schema version '{document.SchemaVersion}'. Supported version is 1.");
        }

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