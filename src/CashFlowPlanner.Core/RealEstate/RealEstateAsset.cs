using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.Core.RealEstate;

public sealed class RealEstateAsset
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public RealEstateType Type { get; init; }

    public decimal CurrentEstimatedValue { get; init; }

    /// <summary>
    /// The date <see cref="CurrentEstimatedValue"/> was established on.
    /// Only meaningful together with <see cref="AnnualValueGrowthPercent"/>:
    /// with no growth the value is flat and the date does not matter.
    /// </summary>
    public DateOnly? ValuationDate { get; init; }

    /// <summary>
    /// Assumed annual change in market value, in percent. Defaults to 0, which
    /// holds the property at <see cref="CurrentEstimatedValue"/> for the whole
    /// horizon -- the previous behaviour, and the only assumption-free one.
    /// </summary>
    public decimal AnnualValueGrowthPercent { get; init; }

    /// <summary>
    /// Pillar 2 (BVG) amount originally used for this property.
    /// Important: this becomes purpose-bound again on sale.
    /// </summary>
    public decimal Pillar2BvgUsedAmount { get; init; }

    /// <summary>
    /// Linked mortgages (read-only usage for simulator).
    /// </summary>
    public List<Guid> LinkedMortgageIds { get; init; } = [];

    public string? Notes { get; init; }

    /// <summary>
    /// The assumed market value on <paramref name="date"/>.
    ///
    /// The asset is treated as owned for the entire simulated horizon: there is
    /// no acquisition or disposal date, because a purchase that appears in the
    /// net-worth series without the matching cash leg would create wealth out of
    /// nothing. Buying and selling inside the horizon belongs to the house-buy
    /// and house-sale scenarios, not here.
    /// </summary>
    public decimal GetValueOn(DateOnly date)
    {
        if (AnnualValueGrowthPercent == 0m || ValuationDate is null)
        {
            return CurrentEstimatedValue;
        }

        return AnnualCompounding.Index(
            CurrentEstimatedValue,
            AnnualValueGrowthPercent,
            ValuationDate.Value,
            date);
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Real estate asset name is required.");
        }

        if (CurrentEstimatedValue < 0m)
        {
            throw new InvalidOperationException(
                $"Real estate asset '{Name}' has a negative estimated value.");
        }

        if (Pillar2BvgUsedAmount < 0m)
        {
            throw new InvalidOperationException(
                $"Real estate asset '{Name}' has a negative Pillar 2 (BVG) withdrawal amount.");
        }

        if (AnnualValueGrowthPercent != 0m && ValuationDate is null)
        {
            throw new InvalidOperationException(
                $"Real estate asset '{Name}' assumes {AnnualValueGrowthPercent:N2}% annual value growth " +
                "but states no valuation date to compound it from.");
        }

        var duplicateMortgageIds = LinkedMortgageIds
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        if (duplicateMortgageIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"Real estate asset '{Name}' links the same mortgage more than once: " +
                $"{string.Join(", ", duplicateMortgageIds)}.");
        }
    }
}

public enum RealEstateType
{
    House,
    Flat
}
