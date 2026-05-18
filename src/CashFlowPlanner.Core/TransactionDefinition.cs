namespace CashFlowPlanner.Core;

public sealed class TransactionDefinition
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public TransactionKind Kind { get; init; }

    public Guid? FromAccountId { get; init; }

    public Guid? ToAccountId { get; init; }

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "CHF";

    public required Schedule Schedule { get; init; }

    public string? Category { get; init; }

    public string? Counterparty { get; init; }

    public Guid? IncomePersonId { get; set; }

    public PaymentMethod PaymentMethod { get; init; } = PaymentMethod.Unknown;

    /// <summary>
    /// Lower values are applied earlier on the same date.
    /// Example:
    /// salary = 10,
    /// savings transfer = 50,
    /// external payments = 100.
    /// </summary>
    public int Priority { get; init; } = 100;

    public bool IsActive { get; init; } = true;

    public string? Notes { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Transaction name is required.");
        }

        if (Amount <= 0m)
        {
            throw new InvalidOperationException($"Transaction '{Name}' requires a positive amount.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new InvalidOperationException($"Transaction '{Name}' requires a currency.");
        }

        Schedule.Validate();

        switch (Kind)
        {
            case TransactionKind.ExternalIncome:
                if (ToAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind ExternalIncome requires ToAccountId.");
                }
                break;

            case TransactionKind.ExternalExpense:
                if (FromAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind ExternalExpense requires FromAccountId.");
                }
                break;

            case TransactionKind.InternalTransfer:
                if (FromAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind InternalTransfer requires FromAccountId.");
                }

                if (ToAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind InternalTransfer requires ToAccountId.");
                }

                if (FromAccountId == ToAccountId)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' cannot transfer to the same account.");
                }
                break;

            case TransactionKind.DebtIncrease:
                if (ToAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind DebtIncrease requires ToAccountId.");
                }
                break;

            case TransactionKind.DebtPayment:
                if (FromAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind DebtPayment requires FromAccountId.");
                }

                if (ToAccountId is null)
                {
                    throw new InvalidOperationException(
                        $"Transaction '{Name}' of kind DebtPayment requires ToAccountId.");
                }
                break;

            default:
                throw new InvalidOperationException(
                    $"Transaction '{Name}' has unsupported kind '{Kind}'.");
        }
    }


    public override string ToString()
        => $"{Name}: {Amount:N2} {Currency}";
}