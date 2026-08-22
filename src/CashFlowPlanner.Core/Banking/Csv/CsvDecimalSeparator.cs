namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Which character separates the francs from the centimes.
///
/// <para>
/// Only the <i>decimal</i> separator is a profile decision. Grouping is not: apostrophes,
/// spaces of every width and whichever of <c>.</c> and <c>,</c> is not the decimal separator
/// are all stripped unconditionally, so one setting covers <c>1'234.56</c>, <c>1 234.56</c>
/// and <c>1,234.56</c> alike.
/// </para>
/// </summary>
public enum CsvDecimalSeparator
{
    /// <summary>Decide from the shape of the numbers in the file. See <see cref="CsvAmountParser"/>.</summary>
    Auto = 0,

    /// <summary>1'234.56 - the Swiss and English convention.</summary>
    Dot = 1,

    /// <summary>1.234,56 - the German and French convention.</summary>
    Comma = 2
}
