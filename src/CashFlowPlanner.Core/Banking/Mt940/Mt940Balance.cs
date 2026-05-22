namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940Balance
{
    public DateOnly Date { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "CHF";

    public Mt940DebitCreditIndicator DebitCreditIndicator { get; init; }

    public bool IsCredit =>
        DebitCreditIndicator is Mt940DebitCreditIndicator.Credit
            or Mt940DebitCreditIndicator.ReversalOfDebit;
}