namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// One <c>Stmt/Ntry</c> - a single booking on the account, and the only unit that participates
/// in balance arithmetic.
///
/// <para>
/// The entry is first-class; <see cref="Details"/> is optional enrichment. For an internal
/// batch booking <see cref="Amount"/> is the *batch total* and the individual items appear as
/// several <see cref="Details"/>; adding those to the entry amount would count the money twice.
/// The rule this parser enforces everywhere is: sum at entry level, drill into details only for
/// text and references.
/// </para>
/// </summary>
public sealed class Camt053Entry
{
    /// <summary>
    /// <c>AcctSvcrRef</c> - the bank's own reference for the booking. The Swiss Payment Standards
    /// implementation guidelines name this explicitly as the field for duplicate checking at
    /// booking level, which is why it becomes the deduplication key.
    /// </summary>
    public string? AccountServicerReference { get; init; }

    /// <summary><c>NtryRef</c>. Rarely populated in Switzerland; used only as a last-resort reference.</summary>
    public string? EntryReference { get; init; }

    /// <summary><c>BookgDt/Dt</c> (or <c>DtTm</c>).</summary>
    public DateOnly? BookingDate { get; init; }

    /// <summary><c>ValDt/Dt</c> (or <c>DtTm</c>). Falls back to the booking date when absent.</summary>
    public DateOnly ValueDate { get; init; }

    public Camt053CreditDebitIndicator CreditDebitIndicator { get; init; }

    /// <summary>Unsigned <c>Amt</c> exactly as it appears in the file.</summary>
    public decimal Amount { get; init; }

    /// <summary>
    /// <see cref="Amount"/> with the sign from <see cref="CreditDebitIndicator"/> applied.
    /// Negative for DBIT, positive for CRDT.
    /// </summary>
    public decimal SignedAmount =>
        CreditDebitIndicator == Camt053CreditDebitIndicator.Debit
            ? -Amount
            : Amount;

    /// <summary>
    /// Currency from the <c>Ccy</c> *attribute* of <c>Amt</c> - <c>&lt;Amt Ccy="CHF"&gt;123.45&lt;/Amt&gt;</c>,
    /// not a child element. Falls back to <c>Stmt/Acct/Ccy</c> when the attribute is missing.
    /// </summary>
    public string Currency { get; init; } = "CHF";

    /// <summary>
    /// <c>Sts</c> - <c>BOOK</c>, <c>PDNG</c> or <c>INFO</c>. camt.053 carries booked items only,
    /// so anything else is a bank deviating from the IG. Non-booked entries are *not* filtered
    /// out: they stay in the sum, where the balance check turns them into a loud, visible
    /// reconciliation difference rather than a silent omission.
    /// </summary>
    public string? Status { get; init; }

    /// <summary>
    /// <c>RvslInd</c>. Informational only. Per ISO the direction of a reversal is already carried
    /// by <c>CdtDbtInd</c> - a reversed credit arrives as a DBIT entry - so the sign must not be
    /// flipped again here.
    /// </summary>
    public bool IsReversal { get; init; }

    /// <summary>
    /// <c>BkTxCd</c> flattened to <c>Domn-Fmly-SubFmly</c>, e.g. <c>PMNT-RCDT-ESCT</c>. Falls back
    /// to <c>BkTxCd/Prtry/Cd</c> when the bank sends only a proprietary code. This is the best
    /// field in the format for categorising a transaction.
    /// </summary>
    public string BankTransactionCode { get; init; } = string.Empty;

    /// <summary>
    /// <c>Chrgs/TtlChrgsAndTaxAmt</c>. Purely informational: <see cref="Amount"/> is the amount
    /// actually booked to the account and already includes any charge deducted from it, so
    /// charges are never added to or subtracted from the sum. Doing so would break
    /// <c>CLBD - OPBD == sum of signed entry amounts</c>.
    /// </summary>
    public decimal? TotalCharges { get; init; }

    public string? ChargesCurrency { get; init; }

    /// <summary><c>AddtlNtryInf</c> - the bank's free-text line for the whole booking.</summary>
    public string? AdditionalEntryInformation { get; init; }

    /// <summary>
    /// The <c>TxDtls</c> under all <c>NtryDtls</c> of this entry, flattened. Empty is normal and
    /// expected: <c>NtryDtls</c> is <c>0..n</c>.
    /// </summary>
    public IReadOnlyList<Camt053TransactionDetail> Details { get; init; } = [];

    /// <summary>
    /// True when this entry is an internal batch booking: the amount is a total covering several
    /// items that are itemised in <see cref="Details"/>.
    /// </summary>
    public bool IsBatchBooking =>
        Details.Count > 1;

    /// <summary>
    /// The raw <c>&lt;Ntry&gt;</c> element as XML, kept in memory for diagnostics and the import
    /// preview. Deliberately *not* persisted with the imported transaction - see the note on
    /// Raw61/Raw86 in <c>ImportedBankStatementMapper</c>.
    /// </summary>
    public string RawXml { get; init; } = string.Empty;
}
