namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940Transaction
{
    public DateOnly ValueDate { get; init; }

    public DateOnly? BookingDate { get; init; }

    public Mt940DebitCreditIndicator DebitCreditIndicator { get; init; }

    public decimal SignedAmount { get; init; }

    public decimal Amount =>
        Math.Abs(SignedAmount);

    public string Currency { get; init; } = "CHF";

    public string TransactionCode { get; init; } = string.Empty;

    public string? CustomerReference { get; init; }

    public string? BankReference { get; init; }

    public string? SupplementaryDetails { get; init; }

    public string? Structured86Code { get; init; }

    public string? Structured86Text { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Raw61 { get; init; } = string.Empty;

    public string? Raw86 { get; init; }
}