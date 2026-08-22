namespace CashFlowPlanner.Core.Pillar3a;

/// <summary>
/// What <see cref="Pillar3aEventGenerator"/> produced, plus everything it could
/// not model. Mirrors <see cref="Mortgages.MortgageGenerationResult"/>: a
/// generator that can only return events has to either throw or stay silent when
/// a contract is under-specified, and staying silent is how finding H8 survived.
/// </summary>
public sealed class Pillar3aGenerationResult
{
    public required IReadOnlyList<CashFlowEvent> Events { get; init; }

    public required IReadOnlyList<SimulationWarning> Warnings { get; init; }
}
