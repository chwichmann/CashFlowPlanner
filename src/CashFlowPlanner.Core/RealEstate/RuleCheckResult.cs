namespace CashFlowPlanner.Core.RealEstate;

public sealed class RuleCheckResult
{
    public required string Code { get; init; }

    public required string Description { get; init; }

    public bool Passed { get; init; }

    public decimal ActualValue { get; init; }

    public decimal RequiredValue { get; init; }
}