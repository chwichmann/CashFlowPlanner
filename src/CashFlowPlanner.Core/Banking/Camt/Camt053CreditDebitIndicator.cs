namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// ISO 20022 <c>CdtDbtInd</c>. camt never expresses direction with a negative amount, so this
/// is the only source of sign in the whole format.
/// </summary>
public enum Camt053CreditDebitIndicator
{
    /// <summary>CRDT - money into the account.</summary>
    Credit,

    /// <summary>DBIT - money out of the account.</summary>
    Debit
}
