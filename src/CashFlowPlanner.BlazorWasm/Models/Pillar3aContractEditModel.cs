using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class Pillar3aContractEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public Guid? OwnerPersonId { get; set; }

    /// <summary>
    /// The Pillar 3a account this contract's balance is held in.
    ///
    /// Optional in the domain and heavily encouraged in the UI: without it the simulation debits
    /// the payment account and credits nothing, so every franc contributed leaves the plan and the
    /// household looks poorer for saving (finding H8).
    /// </summary>
    public Guid? AccountId { get; set; }

    public Pillar3aContractType Type { get; set; } = Pillar3aContractType.Investment;

    public decimal OpeningValue { get; set; }

    public DateOnly OpeningDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string Currency { get; set; } = "CHF";

    public bool IsActive { get; set; } = true;

    public string? ProviderName { get; set; }

    public string? ContractNumberMasked { get; set; }

    public string? Notes { get; set; }

    public Pillar3aProjectionMethod ProjectionMethod { get; set; } =
        Pillar3aProjectionMethod.ExpectedReturn;

    public decimal ExpectedAnnualReturnPercent { get; set; }

    public decimal AnnualFeePercent { get; set; }

    public decimal? GuaranteedPayoutAtRetirement { get; set; }

    public decimal? ExpectedSurplusPercent { get; set; }

    public List<Pillar3aContributionScheduleEditModel> ContributionSchedules { get; set; } = [];

    public List<Pillar3aWithdrawalEditModel> Withdrawals { get; set; } = [];

    public static Pillar3aContractEditModel FromContract(Pillar3aContract contract)
    {
        return new Pillar3aContractEditModel
        {
            Id = contract.Id,
            Name = contract.Name,
            OwnerPersonId = contract.OwnerPersonId,
            AccountId = contract.AccountId,
            Type = contract.Type,
            OpeningValue = contract.OpeningValue,
            OpeningDate = contract.OpeningDate,
            Currency = contract.Currency,
            IsActive = contract.IsActive,
            ProviderName = contract.ProviderName,
            ContractNumberMasked = contract.ContractNumberMasked,
            Notes = contract.Notes,
            ProjectionMethod = contract.ProjectionAssumption.Method,
            ExpectedAnnualReturnPercent = contract.ProjectionAssumption.ExpectedAnnualReturnPercent,
            AnnualFeePercent = contract.ProjectionAssumption.AnnualFeePercent,
            GuaranteedPayoutAtRetirement = contract.ProjectionAssumption.GuaranteedPayoutAtRetirement,
            ExpectedSurplusPercent = contract.ProjectionAssumption.ExpectedSurplusPercent,
            ContributionSchedules = contract.ContributionSchedules
                .Select(Pillar3aContributionScheduleEditModel.FromSchedule)
                .ToList(),
            Withdrawals = contract.Withdrawals
                .Select(Pillar3aWithdrawalEditModel.FromWithdrawal)
                .ToList()
        };
    }

    public Pillar3aContract ToContract()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Pillar 3a contract name is required.");
        }

        if (OwnerPersonId is null)
        {
            throw new InvalidOperationException("Pillar 3a contract owner is required.");
        }

        return new Pillar3aContract
        {
            Id = Id,
            Name = Name.Trim(),
            OwnerPersonId = OwnerPersonId.Value,
            AccountId = AccountId,
            Type = Type,
            OpeningValue = OpeningValue,
            OpeningDate = OpeningDate,
            Currency = Currency.Trim().ToUpperInvariant(),
            IsActive = IsActive,
            ProviderName = string.IsNullOrWhiteSpace(ProviderName) ? null : ProviderName.Trim(),
            ContractNumberMasked = string.IsNullOrWhiteSpace(ContractNumberMasked) ? null : ContractNumberMasked.Trim(),
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),
            ProjectionAssumption = new Pillar3aProjectionAssumption
            {
                Method = ProjectionMethod,
                ExpectedAnnualReturnPercent = ExpectedAnnualReturnPercent,
                AnnualFeePercent = AnnualFeePercent,
                GuaranteedPayoutAtRetirement = GuaranteedPayoutAtRetirement,
                ExpectedSurplusPercent = ExpectedSurplusPercent
            },
            ContributionSchedules = ContributionSchedules
                .Select(x => x.ToSchedule())
                .ToList(),

            // Was hardcoded to []. Withdrawals were persisted and validated but had no editor, so
            // opening a contract that had any and pressing Save deleted them silently.
            Withdrawals = Withdrawals
                .Select(x => x.ToWithdrawal())
                .ToList()
        };
    }
}
