namespace CashFlowPlanner.Core.Accounts;

public static class AccountInterestCalculator
{
    public static decimal CalculateAnnualInterest(
        decimal balance,
        IReadOnlyCollection<AccountInterestTier> tiers,
        AccountInterestCalculationMethod method)
    {
        if (balance <= 0m)
        {
            return 0m;
        }

        if (tiers.Count == 0)
        {
            return 0m;
        }

        return method switch
        {
            AccountInterestCalculationMethod.FlatBalance =>
                CalculateFlatBalanceInterest(balance, tiers),

            AccountInterestCalculationMethod.TieredBalance =>
                CalculateTieredBalanceInterest(balance, tiers),

            _ =>
                CalculateTieredBalanceInterest(balance, tiers)
        };
    }

    public static decimal CalculateInterestForPeriod(
        decimal balance,
        IReadOnlyCollection<AccountInterestTier> tiers,
        AccountInterestCalculationMethod method,
        InterestDayCountConvention dayCountConvention,
        DateOnly startDateInclusive,
        DateOnly endDateExclusive)
    {
        var annualInterest = CalculateAnnualInterest(
            balance,
            tiers,
            method);

        var yearFraction = InterestDayCountCalculator.GetYearFraction(
            startDateInclusive,
            endDateExclusive,
            dayCountConvention);

        return annualInterest * yearFraction;
    }

    private static decimal CalculateFlatBalanceInterest(
        decimal balance,
        IReadOnlyCollection<AccountInterestTier> tiers)
    {
        var applicableTier = tiers
            .OrderBy(x => x.FromAmount)
            .LastOrDefault(x =>
                balance >= x.FromAmount &&
                (x.ToAmount is null || balance <= x.ToAmount.Value));

        if (applicableTier is null)
        {
            return 0m;
        }

        return balance * applicableTier.AnnualRatePercent / 100m;
    }

    private static decimal CalculateTieredBalanceInterest(
        decimal balance,
        IReadOnlyCollection<AccountInterestTier> tiers)
    {
        var interest = 0m;

        foreach (var tier in tiers.OrderBy(x => x.FromAmount))
        {
            if (balance <= tier.FromAmount)
            {
                continue;
            }

            var tierUpperBound = tier.ToAmount ?? balance;
            var amountInTier = Math.Min(balance, tierUpperBound) - tier.FromAmount;

            if (amountInTier <= 0m)
            {
                continue;
            }

            interest += amountInTier * tier.AnnualRatePercent / 100m;
        }

        return interest;
    }
}