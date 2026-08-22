namespace CashFlowPlanner.Core.Indexation;

/// <summary>
/// Resolves the indexation in force for one <see cref="TransactionDefinition"/>
/// and applies it.
///
/// The rules, in one place so nothing has to re-derive them:
/// <list type="bullet">
/// <item>The rate is the transaction's own when it is
/// <see cref="IndexationMode.Custom"/>, the plan's when it is
/// <see cref="IndexationMode.PlanDefault"/>, and none at all when it is
/// <see cref="IndexationMode.None"/>.</item>
/// <item>The base date is the transaction's own when it states one, otherwise
/// the plan's. A salary last negotiated in 2024 is stated in 2024 francs even
/// though the plan is based on 2026.</item>
/// <item>Nothing is indexed unless a base date is known -- there is no
/// defensible default for "money of when".</item>
/// </list>
/// </summary>
public static class TransactionIndexer
{
    /// <summary>
    /// The rate and base date that govern <paramref name="transaction"/>, or
    /// <c>null</c> when it is not indexed at all.
    /// </summary>
    public static (decimal RatePercent, DateOnly BaseDate)? Resolve(
        TransactionDefinition transaction,
        InflationAssumption? planAssumption)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        var rate = transaction.IndexationMode switch
        {
            IndexationMode.None => 0m,
            IndexationMode.Custom => transaction.AnnualIndexationRatePercent ?? 0m,
            _ => planAssumption?.AnnualRatePercent ?? 0m
        };

        if (rate == 0m)
        {
            return null;
        }

        var baseDate = transaction.IndexationBaseDate ?? planAssumption?.BaseDate;

        return baseDate is null
            ? null
            : (rate, baseDate.Value);
    }

    /// <summary>
    /// The factor <paramref name="transaction"/>'s amount carries on
    /// <paramref name="date"/>. Exactly <c>1</c> when it is not indexed, so an
    /// un-indexed plan produces byte-identical amounts to one that never heard
    /// of inflation.
    /// </summary>
    public static decimal GetFactor(
        TransactionDefinition transaction,
        InflationAssumption? planAssumption,
        DateOnly date)
    {
        var indexation = Resolve(transaction, planAssumption);

        return indexation is null
            ? 1m
            : AnnualCompounding.Factor(
                indexation.Value.RatePercent,
                indexation.Value.BaseDate,
                date);
    }
}
