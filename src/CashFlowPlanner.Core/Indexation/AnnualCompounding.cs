namespace CashFlowPlanner.Core.Indexation;

/// <summary>
/// Annual compounding against a stated base date.
///
/// Everything in this codebase that grows or shrinks at an annual percentage --
/// price inflation, salary progression, real-estate appreciation -- compounds
/// through here so the three cannot drift apart.
///
/// Two deliberate properties:
///
/// 1. Compounding is by COMPLETED YEARS from the base date, not by elapsed days
///    and not per occurrence. A monthly expense stated in 2026 money is charged
///    at the same amount for all twelve months of 2026, steps once on the
///    anniversary, and then holds again for a year. That is how an indexed rent
///    or an indexed wage actually behaves, and it makes the figure a user reads
///    off a chart reproducible by hand.
///
/// 2. The arithmetic is <see cref="decimal"/> throughout -- repeated
///    multiplication rather than <see cref="Math.Pow"/> -- so no binary
///    floating-point error is introduced into money. Nothing here rounds; the
///    caller decides whether a rounding policy applies, because this codebase
///    does not have one.
/// </summary>
public static class AnnualCompounding
{
    /// <summary>
    /// Whole years from <paramref name="baseDate"/> to <paramref name="date"/>,
    /// negative when <paramref name="date"/> is earlier.
    ///
    /// A date inside the first year after the base date answers 0, so an amount
    /// stated "as of" the base date is used verbatim until its first
    /// anniversary.
    /// </summary>
    public static int CompletedYears(DateOnly baseDate, DateOnly date)
    {
        var years = date.Year - baseDate.Year;

        if (date >= baseDate)
        {
            if (date < baseDate.AddYears(years))
            {
                years--;
            }

            return years;
        }

        if (date > baseDate.AddYears(years))
        {
            years++;
        }

        return years;
    }

    /// <summary>
    /// The compounding factor to apply to an amount stated as of
    /// <paramref name="baseDate"/> when it is charged on
    /// <paramref name="date"/>.
    /// A zero rate always answers exactly <c>1</c>.
    /// </summary>
    public static decimal Factor(
        decimal annualRatePercent,
        DateOnly baseDate,
        DateOnly date)
    {
        if (annualRatePercent == 0m)
        {
            return 1m;
        }

        return Pow(1m + (annualRatePercent / 100m), CompletedYears(baseDate, date));
    }

    /// <summary>
    /// <paramref name="amount"/>, stated as of <paramref name="baseDate"/>,
    /// expressed in the money of <paramref name="date"/>.
    /// </summary>
    public static decimal Index(
        decimal amount,
        decimal annualRatePercent,
        DateOnly baseDate,
        DateOnly date)
    {
        return amount * Factor(annualRatePercent, baseDate, date);
    }

    /// <summary>
    /// The inverse of <see cref="Index"/>: <paramref name="amount"/>, observed
    /// on <paramref name="date"/>, expressed back in <paramref name="baseDate"/>
    /// money. This is the "real terms" conversion.
    /// </summary>
    public static decimal Deflate(
        decimal amount,
        decimal annualRatePercent,
        DateOnly baseDate,
        DateOnly date)
    {
        var factor = Factor(annualRatePercent, baseDate, date);

        return factor == 0m
            ? amount
            : amount / factor;
    }

    /// <summary>
    /// <paramref name="value"/> raised to an integer <paramref name="exponent"/>,
    /// in decimal. Negative exponents invert.
    /// </summary>
    public static decimal Pow(decimal value, int exponent)
    {
        if (exponent == 0)
        {
            return 1m;
        }

        if (exponent < 0)
        {
            var positive = Pow(value, -exponent);

            return positive == 0m
                ? 0m
                : 1m / positive;
        }

        var result = 1m;
        var factor = value;
        var remaining = exponent;

        // Exponentiation by squaring: a 40-year horizon is 6 multiplications
        // instead of 40, and every one of them is exact decimal arithmetic.
        while (remaining > 0)
        {
            if ((remaining & 1) == 1)
            {
                result *= factor;
            }

            remaining >>= 1;

            if (remaining > 0)
            {
                factor *= factor;
            }
        }

        return result;
    }
}
