namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountBalancePoint
{
    public DateOnly Date { get; init; }

    public Guid AccountId { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = "CHF";
}