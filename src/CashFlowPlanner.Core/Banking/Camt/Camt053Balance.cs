namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// One <c>Stmt/Bal</c> entry.
///
/// The type code lives in <c>Tp/CdOrPrtry/Cd</c> and is a schema *choice* with
/// <c>Tp/CdOrPrtry/Prtry</c> - both spellings are read. The codes that occur in Swiss
/// camt.053 are:
/// <list type="bullet">
///   <item><c>OPBD</c> - opening booked balance.</item>
///   <item><c>PRCD</c> - previous closing booked balance. Some banks send this instead of
///         OPBD; it is the same number by definition, so it is accepted as a fallback.</item>
///   <item><c>CLBD</c> - closing booked balance. This is the one that must reconcile.</item>
///   <item><c>CLAV</c> - closing *available* balance. Differs from CLBD by holds and credit
///         lines, so it must never be used for reconciliation.</item>
///   <item><c>ITBD</c> - interim booked. Belongs to camt.052 (intraday) and is ignored here.</item>
/// </list>
/// </summary>
public sealed class Camt053Balance
{
    /// <summary>The raw code, e.g. <c>OPBD</c>, <c>CLBD</c>, <c>PRCD</c>.</summary>
    public string TypeCode { get; init; } = string.Empty;

    public DateOnly Date { get; init; }

    public Camt053CreditDebitIndicator CreditDebitIndicator { get; init; }

    /// <summary>Unsigned amount exactly as it appears in the file.</summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// Amount with the sign from <c>CdtDbtInd</c> applied: DBIT on a balance means the account
    /// is overdrawn, which is a negative balance from the account holder's point of view.
    /// </summary>
    public decimal SignedAmount =>
        CreditDebitIndicator == Camt053CreditDebitIndicator.Debit
            ? -Amount
            : Amount;

    public string Currency { get; init; } = "CHF";
}
