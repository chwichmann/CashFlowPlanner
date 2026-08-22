namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// What a column means. This is the vocabulary a profile maps header names onto, and the
/// vocabulary the import preview shows the user - "column 3 was read as the amount" is exactly
/// a <see cref="Amount"/> entry pointing at index 2.
/// </summary>
public enum CsvColumnRole
{
    /// <summary>The date the bank booked the transaction. The date the plan uses when there is no value date.</summary>
    BookingDate = 0,

    /// <summary>Valuta - the date the money counts from. Preferred for the plan when present, matching MT940 and camt.</summary>
    ValueDate = 1,

    /// <summary>One signed amount: negative out, positive in.</summary>
    Amount = 2,

    /// <summary>Belastung / Débit / Debit - money out, normally written unsigned.</summary>
    Debit = 3,

    /// <summary>Gutschrift / Crédit / Credit - money in.</summary>
    Credit = 4,

    /// <summary>A separate column saying which direction an unsigned <see cref="Amount"/> goes: S/H, D/C, +/-.</summary>
    DebitCreditIndicator = 5,

    /// <summary>Buchungstext, Mitteilung, Zahlungszweck - what the transaction was for.</summary>
    Description = 6,

    /// <summary>The other party: payee, payer, beneficiary.</summary>
    Counterparty = 7,

    /// <summary>A reference the bank printed. Enrichment only, never a deduplication key - a standing order repeats the same reference every month.</summary>
    Reference = 8,

    /// <summary>Running balance after the transaction. The only thing that makes reconciliation possible in CSV.</summary>
    Balance = 9,

    /// <summary>Currency code, when the export states one per row.</summary>
    Currency = 10
}
