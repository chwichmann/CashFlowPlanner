namespace CashFlowPlanner.Core.RealEstate;

public sealed class SwissMortgageRuleSettings
{
    public decimal MaxLoanToValuePercent { get; init; } = 80m;

    public decimal MinTotalEquityPercent { get; init; } = 20m;

    public decimal MinHardEquityPercent { get; init; } = 10m;

    public decimal MaxPillar2Percent { get; init; } = 10m;

    public decimal FirstMortgageThresholdPercent { get; init; } = 66m;

    public int AmortisationYears { get; init; } = 15;

    public decimal ImputedInterestPercent { get; init; } = 5m;

    public decimal MaintenancePercent { get; init; } = 1m;

    public decimal MaxAffordabilityPercent { get; init; } = 33m;
}