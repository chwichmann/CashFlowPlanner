namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// How a file says which way the money went. All three conventions are in use in Switzerland,
/// sometimes by the same bank in two different exports.
/// </summary>
public enum CsvAmountConvention
{
    /// <summary>Work it out from which columns were found.</summary>
    Auto = 0,

    /// <summary>One column, sign included: <c>-45.60</c> out, <c>2500.00</c> in.</summary>
    SignedAmount = 1,

    /// <summary>Two columns - Belastung and Gutschrift - each holding an unsigned amount, one of them empty.</summary>
    SeparateDebitCredit = 2,

    /// <summary>One unsigned amount column plus a direction column: S/H, D/C, Soll/Haben.</summary>
    AmountWithIndicator = 3
}
