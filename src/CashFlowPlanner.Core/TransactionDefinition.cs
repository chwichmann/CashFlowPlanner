using CashFlowPlanner.Core.Indexation;

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

    /// <summary>
    /// Whether this transaction follows the plan's inflation assumption, is
    /// exempt from it, or carries its own rate. See <see cref="IndexationMode"/>.
    /// </summary>
    public IndexationMode IndexationMode { get; init; } = IndexationMode.PlanDefault;

    /// <summary>
    /// This transaction's own annual indexation rate, in percent. Required when
    /// <see cref="IndexationMode"/> is <see cref="IndexationMode.Custom"/>,
    /// ignored otherwise.
    /// </summary>
    public decimal? AnnualIndexationRatePercent { get; init; }

    /// <summary>
    /// The date <see cref="Amount"/> is stated in the money of, when that is not
    /// the plan's inflation base date. A salary last negotiated in 2024 is
    /// stated in 2024 francs even in a plan based on 2026.
    /// </summary>
    public DateOnly? IndexationBaseDate { get; init; }

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

        ValidateIndexation();

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


    private void ValidateIndexation()
    {
        if (IndexationMode == IndexationMode.Custom &&
            AnnualIndexationRatePercent is null)
        {
            throw new InvalidOperationException(
                $"Transaction '{Name}' uses a custom indexation rate but does not state one.");
        }

        if (AnnualIndexationRatePercent is not null &&
            AnnualIndexationRatePercent.Value <= -100m)
        {
            throw new InvalidOperationException(
                $"Transaction '{Name}' has an indexation rate of -100% a year or worse.");
        }
    }

    public override string ToString()
        => $"{Name}: {Amount:N2} {Currency}";
}