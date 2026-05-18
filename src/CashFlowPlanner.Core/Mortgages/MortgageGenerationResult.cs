namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageGenerationResult
{
    public required IReadOnlyList<CashFlowEvent> Events { get; init; }

    public required IReadOnlyList<MortgagePrincipalPoint> PrincipalPoints { get; init; }
}