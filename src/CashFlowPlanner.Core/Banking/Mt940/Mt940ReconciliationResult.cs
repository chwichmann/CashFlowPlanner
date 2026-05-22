namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940ReconciliationResult
{
    public bool IsAvailable { get; init; }

    public bool IsBalanced { get; init; }

    public decimal? OpeningBalance { get; init; }

    public decimal? ClosingBalance { get; init; }

    public decimal TransactionNetAmount { get; init; }

    public decimal? ExpectedClosingBalance { get; init; }

    public decimal? Difference { get; init; }

    public string Currency { get; init; } = "CHF";

    public static Mt940ReconciliationResult NotAvailable()
    {
        return new Mt940ReconciliationResult
        {
            IsAvailable = false,
            IsBalanced = false
        };
    }

    public static Mt940ReconciliationResult Create(
        Mt940Balance? openingBalance,
        Mt940Balance? closingBalance,
        IReadOnlyCollection<Mt940Transaction> transactions)
    {
        if (openingBalance is null || closingBalance is null)
        {
            return NotAvailable();
        }

        var transactionNetAmount = transactions.Sum(x => x.SignedAmount);
        var expectedClosingBalance = openingBalance.Amount + transactionNetAmount;
        var difference = closingBalance.Amount - expectedClosingBalance;

        return new Mt940ReconciliationResult
        {
            IsAvailable = true,
            IsBalanced = difference == 0m,
            OpeningBalance = openingBalance.Amount,
            ClosingBalance = closingBalance.Amount,
            TransactionNetAmount = transactionNetAmount,
            ExpectedClosingBalance = expectedClosingBalance,
            Difference = difference,
            Currency = closingBalance.Currency
        };
    }
}