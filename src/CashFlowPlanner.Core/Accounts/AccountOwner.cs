namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountOwner
{
    public Guid PersonId { get; set; }

    public decimal OwnershipShare { get; set; } = 1m;
}