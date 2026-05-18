namespace CashFlowPlanner.Core.RealEstate;

public sealed class HouseSaleResult
{
    public decimal NetProceeds { get; init; }

    public decimal Pillar2BoundAmount { get; init; }

    public decimal FreeCashAmount { get; init; }

    public decimal TotalAvailable => Pillar2BoundAmount + FreeCashAmount;
}