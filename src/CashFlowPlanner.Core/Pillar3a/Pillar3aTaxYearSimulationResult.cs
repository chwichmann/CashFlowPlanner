namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aTaxYearSimulationResult
{
    public int TaxYear { get; init; }

    public decimal AnnualLimitPerPerson { get; init; }

    public IReadOnlyList<Pillar3aTaxYearSimulationPersonResult> Persons { get; init; }
        = [];
}
