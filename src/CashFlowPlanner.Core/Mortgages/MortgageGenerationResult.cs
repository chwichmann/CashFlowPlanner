namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageGenerationResult
{
    public required IReadOnlyList<CashFlowEvent> Events { get; init; }

    public required IReadOnlyList<MortgagePrincipalPoint> PrincipalPoints { get; init; }

    /// <summary>
    /// Advisory findings about the contracts themselves -- for example a
    /// calculation principal whose known-at date does not line up with the
    /// simulation start, so the engine had to roll the principal along the
    /// billing calendar to get there.
    /// </summary>
    public IReadOnlyList<SimulationWarning> Warnings { get; init; } = [];
}