namespace CashFlowPlanner.Core.RealEstate;

public sealed class PersonIncome
{
    public Guid PersonId { get; init; }

    public required string Name { get; init; }

    public decimal GrossAnnualIncome { get; init; }
}