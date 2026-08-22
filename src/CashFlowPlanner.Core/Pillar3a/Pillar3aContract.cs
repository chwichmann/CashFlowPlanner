namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aContract
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public Guid OwnerPersonId { get; init; }

    /// <summary>
    /// The <see cref="CashFlowPlanner.Core.Accounts.AccountType.Pillar3a"/>
    /// account this contract's balance is held in.
    ///
    /// Finding H8: without this link a contribution was posted as an
    /// <see cref="TransactionKind.ExternalExpense"/> -- the payment account was
    /// debited and the money was credited to nothing, so every franc paid into
    /// Pillar 3a left the plan and the household appeared poorer for saving.
    /// With the link the contribution becomes an
    /// <see cref="TransactionKind.InternalTransfer"/>, exactly as a mortgage's
    /// indirect amortisation already was.
    ///
    /// Optional, because plans written before the link existed have no account
    /// to point at. An unlinked contract still simulates, still debits the
    /// payment account, and raises a warning saying the money is not tracked.
    /// </summary>
    public Guid? AccountId { get; init; }

    public Pillar3aContractType Type { get; init; } = Pillar3aContractType.Investment;

    public decimal OpeningValue { get; init; }

    public DateOnly OpeningDate { get; init; }

    public string Currency { get; init; } = "CHF";

    public bool IsActive { get; init; } = true;

    public List<Pillar3aContributionSchedule> ContributionSchedules { get; init; } = [];

    public List<Pillar3aWithdrawalEvent> Withdrawals { get; init; } = [];

    public Pillar3aProjectionAssumption ProjectionAssumption { get; init; } = new();

    public string? ProviderName { get; init; }

    public string? ContractNumberMasked { get; init; }

    public string? Notes { get; init; }

    /// <summary>
    /// The date the earliest closing withdrawal falls on, or <c>null</c> when
    /// the contract is never closed. Contributions stop on that date: paying
    /// into a contract that has been paid out and shut is not a thing that can
    /// happen.
    /// </summary>
    public DateOnly? GetClosingDate()
    {
        DateOnly? closingDate = null;

        foreach (var withdrawal in Withdrawals)
        {
            if (!withdrawal.CloseContract)
            {
                continue;
            }

            if (closingDate is null || withdrawal.Date < closingDate.Value)
            {
                closingDate = withdrawal.Date;
            }
        }

        return closingDate;
    }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Pillar 3a contract name is required.");
        }

        if (OwnerPersonId == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{Name}' requires an owner person.");
        }

        if (OpeningValue < 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{Name}' has a negative opening value.");
        }

        if (string.IsNullOrWhiteSpace(Currency))
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{Name}' requires a currency.");
        }

        foreach (var schedule in ContributionSchedules)
        {
            schedule.Validate(Name);
        }

        foreach (var withdrawal in Withdrawals)
        {
            withdrawal.Validate(Name);
        }

        ProjectionAssumption.Validate(Name);

        ValidateTypeSpecificProjection();
    }

    private void ValidateTypeSpecificProjection()
    {
        if (Type == Pillar3aContractType.Insurance &&
            ProjectionAssumption.Method == Pillar3aProjectionMethod.FixedInterest)
        {
            throw new InvalidOperationException(
                $"Pillar 3a insurance contract '{Name}' should not use fixed-interest projection.");
        }

        if (Type == Pillar3aContractType.BankAccount &&
            ProjectionAssumption.Method == Pillar3aProjectionMethod.InsuranceGuaranteedPayout)
        {
            throw new InvalidOperationException(
                $"Pillar 3a bank account contract '{Name}' should not use insurance guaranteed payout projection.");
        }
    }
}