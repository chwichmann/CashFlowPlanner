namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aWithdrawalEvent
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public DateOnly Date { get; init; }

    public Pillar3aWithdrawalReason Reason { get; init; } = Pillar3aWithdrawalReason.Retirement;

    public decimal? Amount { get; init; }

    public bool CloseContract { get; init; }

    public Guid? TargetAccountId { get; init; }

    public string? Notes { get; init; }

    public void Validate(string contractName)
    {
        if (Amount is not null && Amount.Value <= 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a withdrawal for contract '{contractName}' must have a positive amount.");
        }

        if (!CloseContract && Amount is null)
        {
            throw new InvalidOperationException(
                $"Pillar 3a withdrawal for contract '{contractName}' must define an amount unless it closes the contract.");
        }
    }
}