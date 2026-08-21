namespace CashFlowPlanner.Core.Accounts;

/// <summary>
/// One leg of a posting: the account it hits and the signed amount it moves on
/// that account. A positive amount increases the balance, a negative one
/// decreases it.
/// </summary>
public readonly record struct AccountPostingLeg(
    Guid? AccountId,
    decimal SignedAmount)
{
    /// <summary>
    /// Human-readable name of the operation, used in warning messages.
    /// </summary>
    public string OperationName =>
        SignedAmount >= 0m ? "increase" : "decrease";
}

/// <summary>
/// The single source of truth for how a <see cref="CashFlowEvent"/> changes
/// account balances.
///
/// This used to be re-implemented in four places -- <see cref="SimulationEngine"/>,
/// <see cref="AccountStatementBuilder"/>, <see cref="AccountInterestEventGenerator"/>
/// and the credit-card payment generator -- and they disagreed: two of them applied
/// "To => +, From => -" without ever looking at <see cref="TransactionKind"/>, so a
/// <see cref="TransactionKind.DebtIncrease"/> of 500 read as +500 in the account
/// statement and -500 in the engine.
///
/// Every balance computation must go through this type.
/// </summary>
public static class AccountPosting
{
    /// <summary>
    /// Resolves the postings an event produces.
    /// Returns <c>false</c> for an unsupported <see cref="TransactionKind"/>,
    /// in which case no balance may be changed.
    /// </summary>
    public static bool TryGetLegs(
        CashFlowEvent cashFlowEvent,
        out IReadOnlyList<AccountPostingLeg> legs)
    {
        ArgumentNullException.ThrowIfNull(cashFlowEvent);

        switch (cashFlowEvent.Kind)
        {
            case TransactionKind.ExternalIncome:
                legs =
                [
                    new AccountPostingLeg(cashFlowEvent.ToAccountId, cashFlowEvent.Amount)
                ];
                return true;

            case TransactionKind.ExternalExpense:
                legs =
                [
                    new AccountPostingLeg(cashFlowEvent.FromAccountId, -cashFlowEvent.Amount)
                ];
                return true;

            // A debt increase adds to what is owed, so the liability account moves
            // further negative even though the event points AT that account.
            case TransactionKind.DebtIncrease:
                legs =
                [
                    new AccountPostingLeg(cashFlowEvent.ToAccountId, -cashFlowEvent.Amount)
                ];
                return true;

            case TransactionKind.InternalTransfer:
            case TransactionKind.DebtPayment:
                legs =
                [
                    new AccountPostingLeg(cashFlowEvent.FromAccountId, -cashFlowEvent.Amount),
                    new AccountPostingLeg(cashFlowEvent.ToAccountId, cashFlowEvent.Amount)
                ];
                return true;

            default:
                legs = [];
                return false;
        }
    }

    /// <summary>
    /// The signed amount by which <paramref name="cashFlowEvent"/> changes the
    /// balance of <paramref name="accountId"/>. Zero when the event does not
    /// touch that account, or when its kind is not supported.
    /// </summary>
    public static decimal GetSignedAmount(
        Guid accountId,
        CashFlowEvent cashFlowEvent)
    {
        if (!TryGetLegs(cashFlowEvent, out var legs))
        {
            return 0m;
        }

        var signedAmount = 0m;

        foreach (var leg in legs)
        {
            if (leg.AccountId == accountId)
            {
                signedAmount += leg.SignedAmount;
            }
        }

        return signedAmount;
    }
}
