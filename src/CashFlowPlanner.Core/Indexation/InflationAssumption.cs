namespace CashFlowPlanner.Core.Indexation;

/// <summary>
/// The plan's price-inflation assumption.
///
/// Until this existed there was no inflation anywhere in the domain. Over a
/// 20-to-30 year horizon that understates every projected expense by 35-80% in
/// real terms, which makes long-horizon planning not merely imprecise but
/// actively misleading: the plan says the household can afford a retirement it
/// cannot.
///
/// The default is a rate of zero, which reproduces the previous behaviour
/// exactly -- an existing plan that has never been told a rate keeps the numbers
/// it had.
/// </summary>
public sealed class InflationAssumption
{
    /// <summary>
    /// Assumed annual price inflation, in percent. The Swiss long-run average is
    /// around 1%, but no rate is baked in anywhere: this codebase does not ship
    /// reference data it cannot keep current, so the number is the user's.
    /// </summary>
    public decimal AnnualRatePercent { get; init; }

    /// <summary>
    /// The date every un-overridden transaction amount is stated in the money
    /// of. Amounts entered "in today's francs" want today's date here.
    ///
    /// Required as soon as <see cref="AnnualRatePercent"/> is non-zero: without
    /// it there is nothing to compound from, and defaulting silently to the
    /// simulation start would make a saved plan change meaning when the horizon
    /// moves.
    /// </summary>
    public DateOnly? BaseDate { get; init; }

    public bool IsEnabled => AnnualRatePercent != 0m && BaseDate is not null;

    public void Validate()
    {
        if (AnnualRatePercent <= -100m)
        {
            throw new InvalidOperationException(
                "Inflation rate must be greater than -100% a year.");
        }

        if (AnnualRatePercent != 0m && BaseDate is null)
        {
            throw new InvalidOperationException(
                $"The plan assumes {AnnualRatePercent:N2}% annual inflation but states no base " +
                "date for the amounts it indexes. Set the date the amounts are stated in the " +
                "money of.");
        }
    }
}
