using System.Reflection;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// Supplies the bundled example plan shown on the empty-session screen.
/// <para>
/// The JSON is an <c>EmbeddedResource</c> rather than a file under <c>wwwroot</c> on purpose: this
/// app makes no network requests of any kind, and fetching a static asset - even from its own
/// origin - would mean reintroducing an <c>HttpClient</c>. Reading it out of the assembly keeps
/// that guarantee absolute and testable.
/// </para>
/// </summary>
public sealed class StarterPlanProvider
{
    private const string ResourceName =
        "CashFlowPlanner.BlazorWasm.Resources.StarterPlan.json";

    private string? _cachedJson;

    /// <summary>
    /// The starter plan as JSON, ready to hand to the normal deserialize path so it goes through
    /// exactly the same migration and validation as a user-supplied file.
    /// </summary>
    public string GetJson()
    {
        if (_cachedJson is not null)
        {
            return _cachedJson;
        }

        var assembly = typeof(StarterPlanProvider).GetTypeInfo().Assembly;

        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded starter plan '{ResourceName}' is missing from the assembly. "
                + "Check the EmbeddedResource entry in CashFlowPlanner.BlazorWasm.csproj.");

        using var reader = new StreamReader(stream);

        _cachedJson = reader.ReadToEnd();

        return _cachedJson;
    }
}
