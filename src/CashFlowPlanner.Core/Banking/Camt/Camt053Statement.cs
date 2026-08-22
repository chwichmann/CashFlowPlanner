namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// One <c>BkToCstmrStmt/Stmt</c> - the statement of a single account.
///
/// <para>
/// <c>Stmt</c> is <c>1..n</c>. Swiss Payment Standards *typically* delivers one account per
/// file, which is exactly what makes the assumption dangerous: a single-statement parser works
/// until the bank sends a combined export, and then it silently imports only the first account.
/// Every account-scoped fact - IBAN, currency, balances, sequence number, entries - therefore
/// lives here and not on <see cref="Camt053File"/>, and reconciliation is evaluated per
/// statement.
/// </para>
/// </summary>
public sealed class Camt053Statement
{
    /// <summary><c>Stmt/Id</c>. With <see cref="ElectronicSequenceNumber"/>, identifies the statement for duplicate detection.</summary>
    public string? Id { get; init; }

    /// <summary><c>ElctrncSeqNb</c> - the sequence number of the electronic statement.</summary>
    public string? ElectronicSequenceNumber { get; init; }

    /// <summary><c>LglSeqNb</c> - the sequence number of the paper statement. Used when <c>ElctrncSeqNb</c> is absent.</summary>
    public string? LegalSequenceNumber { get; init; }

    public DateTimeOffset? CreationDateTime { get; init; }

    /// <summary><c>FrToDt/FrDtTm</c>.</summary>
    public DateOnly? FromDate { get; init; }

    /// <summary><c>FrToDt/ToDtTm</c>.</summary>
    public DateOnly? ToDate { get; init; }

    /// <summary><c>Acct/Id/IBAN</c>. The identifier the statement is matched to an account with.</summary>
    public string? Iban { get; init; }

    /// <summary><c>Acct/Id/Othr/Id</c> - the fallback when the account has no IBAN (rare in Switzerland).</summary>
    public string? OtherAccountIdentification { get; init; }

    /// <summary>
    /// The account identifier used for matching and for stamping onto imported transactions:
    /// the IBAN when present, otherwise the proprietary identification.
    /// </summary>
    public string? AccountIdentifier =>
        Iban ?? OtherAccountIdentification;

    /// <summary><c>Acct/Ccy</c> - the fallback for entries whose <c>Amt</c> carries no <c>Ccy</c> attribute.</summary>
    public string Currency { get; init; } = "CHF";

    public string? AccountOwnerName { get; init; }

    public string? ServicerBic { get; init; }

    /// <summary>All <c>Bal</c> elements, in document order, whatever their type code.</summary>
    public IReadOnlyList<Camt053Balance> Balances { get; init; } = [];

    /// <summary><c>Bal</c> with type <c>OPBD</c>, falling back to <c>PRCD</c>.</summary>
    public Camt053Balance? OpeningBalance { get; init; }

    /// <summary><c>Bal</c> with type <c>CLBD</c>. Never <c>CLAV</c>, which includes holds and credit lines.</summary>
    public Camt053Balance? ClosingBalance { get; init; }

    public IReadOnlyList<Camt053Entry> Entries { get; init; } = [];

    public Camt053ReconciliationResult Reconciliation { get; init; } =
        Camt053ReconciliationResult.NotAvailable();

    /// <summary>Sum of the signed entry amounts. Entry level only - details are never added in.</summary>
    public decimal EntryNetAmount =>
        Entries.Sum(x => x.SignedAmount);
}
