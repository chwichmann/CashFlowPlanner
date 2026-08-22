namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// One <c>Ntry/NtryDtls/TxDtls</c>.
///
/// A transaction detail is *optional enrichment*, never the unit of account. <c>NtryDtls</c>
/// is <c>0..n</c> and <c>TxDtls</c> is <c>0..n</c> within it, so:
/// <list type="bullet">
///   <item>Some banks deliver camt.053 with no detail at all - the entry is all there is.</item>
///   <item>An *internal* batch booking has one entry whose <c>Amt</c> is the batch total and
///         several <c>TxDtls</c> carrying the individual items.</item>
///   <item>An *external* batch booking has one entry with only the total; the items arrive in
///         a separate camt.054.</item>
/// </list>
/// Summing entry amounts and detail amounts together therefore double-counts the internal
/// case. Balances are reconciled at entry level only - see <see cref="Camt053Entry"/>.
/// </summary>
public sealed class Camt053TransactionDetail
{
    /// <summary><c>Refs/AcctSvcrRef</c>. The Swiss IG requires this to always be supplied at detail level.</summary>
    public string? AccountServicerReference { get; init; }

    /// <summary><c>Refs/EndToEndId</c>. Only meaningful for customer-initiated payments; often <c>NOTPROVIDED</c>.</summary>
    public string? EndToEndId { get; init; }

    /// <summary><c>Refs/InstrId</c>.</summary>
    public string? InstructionId { get; init; }

    /// <summary><c>Refs/MndtId</c> - the LSV+/direct-debit mandate.</summary>
    public string? MandateId { get; init; }

    /// <summary><c>Refs/UETR</c>. Present in SWIFT gpi traffic, sparse in Swiss retail.</summary>
    public string? Uetr { get; init; }

    /// <summary>
    /// <c>TxDtls/Amt</c> when present, otherwise <c>null</c>. Never added to the entry amount.
    /// </summary>
    public decimal? Amount { get; init; }

    public Camt053CreditDebitIndicator? CreditDebitIndicator { get; init; }

    /// <summary>Detail amount with the detail's own <c>CdtDbtInd</c> applied, when both are present.</summary>
    public decimal? SignedAmount =>
        Amount is null
            ? null
            : CreditDebitIndicator == Camt053CreditDebitIndicator.Debit
                ? -Amount.Value
                : Amount.Value;

    public string? Currency { get; init; }

    /// <summary>
    /// The structured creditor reference type. <c>Tp/CdOrPrtry</c> is a schema *choice*, so both
    /// spellings are read:
    /// <list type="bullet">
    ///   <item><c>Prtry</c> = <c>QRR</c> with a QR-IBAN - <see cref="CreditorReference"/> is the
    ///         27-digit QR reference.</item>
    ///   <item><c>Cd</c> = <c>SCOR</c> with a normal IBAN - an ISO 11649 <c>RFxx...</c> reference.</item>
    ///   <item><c>Prtry</c> = <c>ISR Reference</c> for LSV+/BDD and legacy ISR slips.</item>
    /// </list>
    /// </summary>
    public string? CreditorReferenceType { get; init; }

    /// <summary><c>RmtInf/Strd/CdtrRefInf/Ref</c>.</summary>
    public string? CreditorReference { get; init; }

    /// <summary><c>RmtInf/Ustrd</c>, all occurrences joined. Where the text goes when there is no structured reference.</summary>
    public string? UnstructuredRemittanceInformation { get; init; }

    /// <summary><c>AddtlTxInf</c> - the bank's free-text line for this item.</summary>
    public string? AdditionalTransactionInformation { get; init; }

    public string? CreditorName { get; init; }

    public string? CreditorIban { get; init; }

    public string? DebtorName { get; init; }

    public string? DebtorIban { get; init; }

    /// <summary><c>Chrgs/TtlChrgsAndTaxAmt</c> at detail level. Informational - see <see cref="Camt053Entry.TotalCharges"/>.</summary>
    public decimal? TotalCharges { get; init; }

    /// <summary>
    /// The counterparty of this item, from the account holder's point of view: for money in it
    /// is the debtor, for money out it is the creditor.
    /// </summary>
    public string? CounterpartyName =>
        CreditDebitIndicator == Camt053CreditDebitIndicator.Debit
            ? CreditorName ?? DebtorName
            : DebtorName ?? CreditorName;
}
