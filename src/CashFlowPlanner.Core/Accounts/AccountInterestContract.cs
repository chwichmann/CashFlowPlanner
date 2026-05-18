namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountInterestContract
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public AccountInterestCalculationMethod CalculationMethod { get; init; }
        = AccountInterestCalculationMethod.TieredBalance;

    public InterestPostingFrequency PostingFrequency { get; init; }
        = InterestPostingFrequency.Yearly;

    public InterestDayCountConvention DayCountConvention { get; init; }
        = InterestDayCountConvention.Actual360;

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public List<AccountInterestTier> Tiers { get; init; } = [];

    public bool IsActive { get; init; } = true;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Interest contract name is required.");
        }

        if (EndDate is not null && EndDate.Value < StartDate)
        {
            throw new InvalidOperationException(
                $"Interest contract '{Name}' has an end date before the start date.");
        }

        if (Tiers.Count == 0)
        {
            throw new InvalidOperationException(
                $"Interest contract '{Name}' requires at least one interest tier.");
        }

        foreach (var tier in Tiers)
        {
            if (tier.FromAmount < 0m)
            {
                throw new InvalidOperationException(
                    $"Interest contract '{Name}' contains a tier with a negative start amount.");
            }

            if (tier.ToAmount is not null && tier.ToAmount.Value <= tier.FromAmount)
            {
                throw new InvalidOperationException(
                    $"Interest contract '{Name}' contains an invalid tier range.");
            }

            if (tier.AnnualRatePercent < 0m)
            {
                throw new InvalidOperationException(
                    $"Interest contract '{Name}' contains a negative interest rate.");
            }
        }

        var ordered = Tiers
            .OrderBy(x => x.FromAmount)
            .ToList();

        for (var i = 1; i < ordered.Count; i++)
        {
            if (ordered[i].FromAmount < ordered[i - 1].ToAmount)
            {
                throw new InvalidOperationException(
                    $"Interest contract '{Name}' contains overlapping interest tiers.");
            }
        }
    }
}