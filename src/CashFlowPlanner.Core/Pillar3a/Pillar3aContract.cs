namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aContract
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public Guid OwnerPersonId { get; init; }

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