namespace CashFlowPlanner.Core.RealEstate;

public sealed class EquitySource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public EquitySourceType Type { get; init; }

    public decimal Amount { get; init; }

    public Guid? PersonId { get; init; }

    public EquitySourceOrigin Origin { get; init; } = EquitySourceOrigin.Manual;
}

public enum EquitySourceType
{
    Cash,
    Pillar2Bvg
}

public enum EquitySourceOrigin
{
    Manual,
    SaleFreeCash,
    SalePillar2
}