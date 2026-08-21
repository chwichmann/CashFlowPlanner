using CashFlowPlanner.Core;

namespace CashFlowPlanner.Storage.Json.Tests;

/// <summary>
/// Finding P2c: SimulationSettings.DateMode defaults to RollingHorizon, which ignores the stored
/// start/end dates. Files written before dateMode existed store a range and no mode, and were
/// silently simulated over a rolling 12 months instead.
/// </summary>
public sealed class ExplicitDateRangeMigrationTests
{
    private static string ShippedSamplePath =>
        Path.Combine(AppContext.BaseDirectory, "Samples", "private-cashflow.sample.json");

    [Fact]
    public void ShippedSample_Should_LoadAsAnExplicitDateRange()
    {
        // Arrange
        var json = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var plan = serializer.DeserializePlan(json);

        // Assert - the sample stores 2026-06-01 -> 2031-12-31 and no dateMode.
        Assert.Equal(SimulationDateMode.ExplicitDateRange, plan.SimulationSettings.DateMode);
        Assert.Equal(new DateOnly(2026, 6, 1), plan.SimulationSettings.StartDate);
        Assert.Equal(new DateOnly(2031, 12, 31), plan.SimulationSettings.EndDate);
    }

    [Fact]
    public void ShippedSample_Should_SimulateTheStoredRangeNotARollingYear()
    {
        // Arrange
        var json = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        var plan = serializer.DeserializePlan(json);

        // Act
        var range = plan.SimulationSettings.GetEffectiveDateRange(
            DateOnly.FromDateTime(DateTime.Today));

        // Assert
        Assert.Equal(new DateOnly(2026, 6, 1), range.StartDate);
        Assert.Equal(new DateOnly(2031, 12, 31), range.EndDate);
    }

    [Fact]
    public void MissingDateMode_WithStoredDates_Should_MigrateToExplicitDateRange()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 1,
          "planId": "00000000-0000-0000-0000-000000000001",
          "name": "Private Cashflow",
          "baseCurrency": "CHF",
          "accounts": [],
          "transactions": [],
          "simulationSettings": {
            "startDate": "2027-01-01",
            "endDate": "2030-12-31",
            "granularity": "Daily"
          }
        }
        """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(json);

        // Assert
        Assert.Equal(
            SimulationDateMode.ExplicitDateRange,
            document.SimulationSettings.DateMode);
    }

    [Fact]
    public void ExplicitRollingHorizon_Should_BeLeftAlone()
    {
        // Arrange - the user deliberately chose a rolling horizon; stored dates are stale leftovers.
        var json = """
        {
          "schemaVersion": 1,
          "planId": "00000000-0000-0000-0000-000000000001",
          "name": "Private Cashflow",
          "baseCurrency": "CHF",
          "accounts": [],
          "transactions": [],
          "simulationSettings": {
            "dateMode": "RollingHorizon",
            "startDate": "2027-01-01",
            "endDate": "2030-12-31",
            "horizonMonths": 24
          }
        }
        """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(json);

        // Assert
        Assert.Equal(SimulationDateMode.RollingHorizon, document.SimulationSettings.DateMode);
        Assert.Equal(24, document.SimulationSettings.HorizonMonths);
    }

    [Fact]
    public void MissingDateMode_WithoutStoredDates_Should_KeepTheRollingHorizonDefault()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 1,
          "planId": "00000000-0000-0000-0000-000000000001",
          "name": "Private Cashflow",
          "baseCurrency": "CHF",
          "accounts": [],
          "transactions": [],
          "simulationSettings": {
            "granularity": "Daily"
          }
        }
        """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var document = serializer.DeserializeDocument(json);

        // Assert
        Assert.Equal(SimulationDateMode.RollingHorizon, document.SimulationSettings.DateMode);
    }

    [Fact]
    public void MigratedDateMode_Should_BeWrittenBackExplicitly()
    {
        // Arrange
        var json = File.ReadAllText(ShippedSamplePath);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var savedJson = serializer.SerializeDocument(serializer.DeserializeDocument(json));

        // Assert - the next load no longer depends on the migration.
        Assert.Contains("\"dateMode\": \"ExplicitDateRange\"", savedJson);

        var reloaded = serializer.DeserializePlan(savedJson);

        Assert.Equal(SimulationDateMode.ExplicitDateRange, reloaded.SimulationSettings.DateMode);
        Assert.Equal(new DateOnly(2031, 12, 31), reloaded.SimulationSettings.EndDate);
    }
}
