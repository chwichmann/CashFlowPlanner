namespace CashFlowPlanner.Core.Indexation;

/// <summary>
/// How one <see cref="TransactionDefinition"/> reacts to the plan's inflation
/// assumption. Not everything indexes, so a single plan-wide rate applied to
/// everything would be as wrong as no rate at all: rent and groceries track
/// prices, a fixed-rate mortgage instalment and a fixed insurance premium do
/// not, and a salary follows its own path.
/// </summary>
public enum IndexationMode
{
    /// <summary>
    /// Follow the plan's <see cref="InflationAssumption"/>. The default, so
    /// turning inflation on at plan level moves everything that has not
    /// explicitly opted out.
    /// </summary>
    PlanDefault = 0,

    /// <summary>
    /// Never indexed. The amount is nominal and fixed for the whole horizon --
    /// a fixed-rate mortgage payment, a fixed-term insurance premium, a
    /// contractual instalment.
    /// </summary>
    None = 1,

    /// <summary>
    /// Indexed at this transaction's own rate rather than the plan's. A salary
    /// rising 2.5% a year against 1% general inflation is the same mechanism
    /// seen from the income side.
    /// </summary>
    Custom = 2
}

/// <summary>
/// Which money a reported figure is expressed in. This is a presentation
/// choice, never a change to what the engine computed: the engine always
/// produces nominal amounts, and <see cref="AmountBasis.Real"/> deflates them
/// back to the plan's inflation base date for display.
/// </summary>
public enum AmountBasis
{
    /// <summary>
    /// Francs of the day the money moves -- what a bank statement in that year
    /// would show.
    /// </summary>
    Nominal = 0,

    /// <summary>
    /// Francs of the plan's inflation base date -- what the amount is worth in
    /// today's purchasing power.
    /// </summary>
    Real = 1
}
