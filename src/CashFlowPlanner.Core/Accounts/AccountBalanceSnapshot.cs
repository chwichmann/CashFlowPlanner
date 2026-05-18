namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountBalanceSnapshot
{
    public DateOnly Date { get; init; }

    public required IReadOnlyDictionary<Guid, decimal> BalancesByAccountId { get; init; }
}