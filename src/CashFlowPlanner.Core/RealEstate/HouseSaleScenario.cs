namespace CashFlowPlanner.Core.RealEstate;

public sealed class HouseSaleScenario
{
    public Guid? RealEstateAssetId { get; init; }

    public DateOnly SaleDate { get; init; }

    public decimal ExpectedSalePrice { get; init; }

    public decimal SellingCosts { get; init; }

    /// <summary>
    /// Remaining mortgage at sale date.
    /// You will later fetch this via SimulationResult.GetMortgagePrincipal(...)
    /// </summary>
    public decimal RemainingMortgagePrincipal { get; init; }

    /// <summary>
    /// From RealEstateAsset
    /// </summary>
    public decimal Pillar2BvgBoundAmount { get; init; }

    public void Validate()
    {
        if (ExpectedSalePrice <= 0)
            throw new InvalidOperationException("Expected sale price must be > 0.");

        if (RemainingMortgagePrincipal < 0)
            throw new InvalidOperationException("Remaining mortgage must not be negative.");

        if (SellingCosts < 0)
            throw new InvalidOperationException("Selling costs must not be negative.");
    }
}
