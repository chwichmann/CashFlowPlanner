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

    /// <summary>
    /// When the household takes ownership. Null means "owned for the whole
    /// horizon", which is the default and the case for a property somebody
    /// already lives in.
    ///
    /// Set it when the purchase happens inside the horizon. The property then
    /// appears on the balance sheet on that day, alongside the mortgage that
    /// paid for it and the cash that left the account -- the three legs net to
    /// roughly zero, which is what a purchase actually is. Set the mortgage's
    /// <c>InitialDate</c> to the same day; a mortgage counts from the day it
    /// starts, and an asset whose debt appears on a different date makes the
    /// series jump.
    /// </summary>
    public DateOnly? AcquisitionDate { get; init; }

    /// <summary>
    /// When the household ceases to own it. Null means "held to the end of the
    /// horizon". The asset stops counting on this date; the sale proceeds and
    /// the mortgage repayment are cash-flow events, and this collection does not
    /// generate them.
    /// </summary>
    public DateOnly? DisposalDate { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// Whether the property is on the household's balance sheet on
    /// <paramref name="date"/>. With no acquisition or disposal date - the
    /// default - it always is, which is the previous behaviour exactly.
    /// <para>
    /// A property acquired mid-horizon with no matching cash and mortgage legs
    /// makes net worth step up by its whole value on that day. That is visible
    /// and explicable; counting a house the household has not bought yet from
    /// the first day of the plan is neither, and it is what this replaces.
    /// </para>
    /// </summary>
    public bool IsOwnedOn(DateOnly date)
    {
        if (AcquisitionDate is not null && date < AcquisitionDate.Value)
        {
            return false;
        }

        return DisposalDate is null || date < DisposalDate.Value;
    }

    /// <summary>
    /// The assumed market value on <paramref name="date"/>.
    ///
    /// This is the market value only. Whether the household owns it on that date
    /// is <see cref="IsOwnedOn"/>, and the net-worth series asks both.
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

        if (AcquisitionDate is not null
            && DisposalDate is not null
            && DisposalDate.Value <= AcquisitionDate.Value)
        {
            throw new InvalidOperationException(
                $"Real estate asset '{Name}' is disposed of on {DisposalDate:yyyy-MM-dd}, " +
                $"on or before it is acquired on {AcquisitionDate:yyyy-MM-dd}.");
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
