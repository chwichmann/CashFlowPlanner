namespace CashFlowPlanner.Core.RealEstate;

public sealed class HouseBuyScenarioManager
{
    public IReadOnlyList<HouseBuySimulatorScenario> AddOrUpdate(
        IReadOnlyCollection<HouseBuySimulatorScenario> existingScenarios,
        HouseBuySimulatorScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(existingScenarios);
        ArgumentNullException.ThrowIfNull(scenario);

        scenario.Validate();

        var scenarios = existingScenarios.ToList();

        var existingIndex = scenarios.FindIndex(x => x.Id == scenario.Id);

        var uniqueName = EnsureUniqueName(
            scenario.Name,
            scenarios,
            scenario.Id);

        var updated = CloneWithNameAndTimestamp(scenario, uniqueName);

        if (existingIndex >= 0)
        {
            scenarios[existingIndex] = updated;
        }
        else
        {
            scenarios.Add(updated);
        }

        ValidateScenarioList(scenarios);

        return scenarios;
    }

    public IReadOnlyList<HouseBuySimulatorScenario> Delete(
        IReadOnlyCollection<HouseBuySimulatorScenario> existingScenarios,
        Guid scenarioId)
    {
        ArgumentNullException.ThrowIfNull(existingScenarios);

        var scenarios = existingScenarios
            .Where(x => x.Id != scenarioId)
            .ToList();

        return scenarios;
    }

    public HouseBuySimulatorScenario GetRequired(
        IReadOnlyCollection<HouseBuySimulatorScenario> scenarios,
        Guid scenarioId)
    {
        var scenario = scenarios.SingleOrDefault(x => x.Id == scenarioId);

        if (scenario is null)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioId}' not found.");
        }

        return scenario;
    }

    public HouseBuySimulatorScenario CreateDefault(
        IReadOnlyCollection<HouseBuySimulatorScenario> existingScenarios)
    {
        var name = EnsureUniqueName(
            "House buy scenario",
            existingScenarios,
            null);

        return HouseBuySimulatorScenario.CreateDefault(name);
    }

    public HouseBuyScenarioDuplicateResult Duplicate(
        IReadOnlyCollection<HouseBuySimulatorScenario> existingScenarios,
        Guid sourceScenarioId)
    {
        var source = existingScenarios.SingleOrDefault(x => x.Id == sourceScenarioId);

        if (source is null)
        {
            throw new InvalidOperationException("Scenario not found.");
        }

        var name = EnsureUniqueName(
            $"{source.Name} copy",
            existingScenarios,
            null);

        var copy = source.CopyAsNew(name);

        var list = existingScenarios.ToList();
        list.Add(copy);

        return new HouseBuyScenarioDuplicateResult
        {
            Scenario = copy,
            Scenarios = list
        };
    }

    public IReadOnlyList<HouseBuySimulatorScenario> Rename(
    IReadOnlyCollection<HouseBuySimulatorScenario> scenarios,
    Guid scenarioId,
    string name)
    {
        ArgumentNullException.ThrowIfNull(scenarios);

        if (scenarioId == Guid.Empty)
        {
            throw new InvalidOperationException("Scenario id is required.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Scenario name is required.");
        }

        var list = scenarios.ToList();

        var index = list.FindIndex(x => x.Id == scenarioId);

        if (index < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioId}' was not found.");
        }

        var uniqueName = EnsureUniqueName(
            name.Trim(),
            list,
            scenarioId);

        var existing = list[index];

        list[index] = CloneWithNameAndTimestamp(existing, uniqueName);

        ValidateScenarioList(list);

        return list;
    }

    private static HouseBuySimulatorScenario CloneWithNameAndTimestamp(
        HouseBuySimulatorScenario source,
        string name)
    {
        return new HouseBuySimulatorScenario
        {
            Id = source.Id,
            Name = name.Trim(),
            CreatedUtc = source.CreatedUtc,
            ModifiedUtc = DateTimeOffset.UtcNow,

            SalePrice = source.SalePrice,
            SaleRemainingMortgage = source.SaleRemainingMortgage,
            SaleCosts = source.SaleCosts,
            SalePillar2BoundAmount = source.SalePillar2BoundAmount,

            BuyPrice = source.BuyPrice,
            RenovationPrice = source.RenovationPrice,
            DesiredMortgage = source.DesiredMortgage,

            Persons = source.Persons,
            EquitySources = source.EquitySources,
            Rules = source.Rules
        };
    }

    private static string EnsureUniqueName(
        string requestedName,
        IReadOnlyCollection<HouseBuySimulatorScenario> existing,
        Guid? ignoreId)
    {
        var baseName = string.IsNullOrWhiteSpace(requestedName)
            ? "House buy scenario"
            : requestedName.Trim();

        var names = existing
            .Where(x => ignoreId is null || x.Id != ignoreId.Value)
            .Select(x => x.Name.Trim().ToUpperInvariant())
            .ToHashSet();

        if (!names.Contains(baseName.ToUpperInvariant()))
        {
            return baseName;
        }

        for (int i = 2; i < 10_000; i++)
        {
            var candidate = $"{baseName} {i}";

            if (!names.Contains(candidate.ToUpperInvariant()))
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not generate unique scenario name.");
    }

    private static void ValidateScenarioList(
        IReadOnlyCollection<HouseBuySimulatorScenario> scenarios)
    {
        foreach (var scenario in scenarios)
        {
            scenario.Validate();
        }
    }
}

public sealed class HouseBuyScenarioDuplicateResult
{
    public required HouseBuySimulatorScenario Scenario { get; init; }
    public required IReadOnlyList<HouseBuySimulatorScenario> Scenarios { get; init; }
}