namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aTaxYearSimulationPersonResult
{
    public Guid PersonId { get; init; }

    public int TaxYear { get; init; }

    public decimal AnnualLimit { get; init; }

    public decimal ScheduledContributions { get; init; }

    public decimal Remaining { get; init; }

    public decimal Excess { get; init; }

    public bool IsLimitReached { get; init; }

    public bool IsExceeded { get; init; }

    public int ContractCount { get; init; }

    public int ActiveScheduleCount { get; init; }
}