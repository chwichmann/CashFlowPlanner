using System.Text.Json.Nodes;

namespace CashFlowPlanner.Storage.Json.Migrations;

/// <summary>
/// Migration #1 (finding P2c).
///
/// <see cref="Core.SimulationSettings.DateMode"/> defaults to
/// <see cref="Core.SimulationDateMode.RollingHorizon"/>, which ignores the stored
/// <c>startDate</c>/<c>endDate</c> entirely. Files written before <c>dateMode</c> existed - the
/// shipped sample among them - store an explicit range and nothing else, so loading one silently
/// replaced the user's 2026-06-01 - 2031-12-31 horizon with a rolling 12 months, displayed the
/// stored dates in the UI anyway, and then wrote the override back to disk on the next save.
///
/// The fix belongs at the storage boundary rather than in the domain default: only the file knows
/// whether the mode was genuinely absent.
/// </summary>
public sealed class ExplicitDateRangeMigration : ICashFlowPlanDocumentMigration
{
    private const string SimulationSettingsPropertyName = "simulationSettings";
    private const string DateModePropertyName = "dateMode";
    private const string StartDatePropertyName = "startDate";
    private const string EndDatePropertyName = "endDate";
    private const string ExplicitDateRange = "ExplicitDateRange";

    public int AppliesToSchemaVersionUpTo => 1;

    public string Description =>
        "Stored simulation start/end dates without a date mode are read as an explicit date range.";

    public bool Apply(JsonObject document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var settingsNode = CashFlowPlanDocumentMigrator.FindProperty(
            document,
            SimulationSettingsPropertyName);

        if (settingsNode is not JsonObject settings)
        {
            return false;
        }

        // An explicit mode - whatever it says - is the user's decision and is left alone.
        if (CashFlowPlanDocumentMigrator.HasProperty(settings, DateModePropertyName))
        {
            return false;
        }

        if (!HasDate(settings, StartDatePropertyName) ||
            !HasDate(settings, EndDatePropertyName))
        {
            return false;
        }

        CashFlowPlanDocumentMigrator.SetProperty(
            settings,
            DateModePropertyName,
            JsonValue.Create(ExplicitDateRange));

        return true;
    }

    private static bool HasDate(JsonObject settings, string propertyName)
    {
        var node = CashFlowPlanDocumentMigrator.FindProperty(settings, propertyName);

        return node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            !string.IsNullOrWhiteSpace(text);
    }
}
