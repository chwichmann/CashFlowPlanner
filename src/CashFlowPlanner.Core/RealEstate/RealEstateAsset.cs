namespace CashFlowPlanner.Core.RealEstate;

public sealed class RealEstateAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public RealEstateType Type { get; init; }

    public decimal CurrentEstimatedValue { get; init; }

    /// <summary>
    /// Pillar 2 (BVG) amount originally used for this property.
    /// Important: this becomes purpose-bound again on sale.
    /// </summary>
    public decimal Pillar2BvgUsedAmount { get; init; }

    /// <summary>
    /// Linked mortgages (read-only usage for simulator).
    /// </summary>
    public List<Guid> LinkedMortgageIds { get; init; } = [];
}

public enum RealEstateType
{
    House,
    Flat
}