namespace CashFlowPlanner.Core.RealEstate;

public sealed class HousePurchaseResult
{
    public decimal TotalPrice { get; init; }

    public decimal CashEquity { get; init; }
    public decimal Pillar2Equity { get; init; }
    public decimal TotalEquity { get; init; }

    public decimal LoanToValuePercent { get; init; }

    public decimal TheoreticalYearlyCost { get; init; }
    public decimal AffordabilityRatio { get; init; }

    public bool IsAffordable { get; init; }

    public decimal GrossAnnualIncome { get; init; }

    public decimal MaxAllowedYearlyCost { get; init; }

    public decimal RequiredGrossAnnualIncomeForAffordability { get; init; }

    public decimal MissingGrossAnnualIncomeForAffordability { get; init; }

    public List<RuleCheckResult> Checks { get; init; } = [];

    public bool IsViable => Checks.All(x => x.Passed);
}