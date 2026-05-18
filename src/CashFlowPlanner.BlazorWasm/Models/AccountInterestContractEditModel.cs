using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class AccountInterestContractEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public AccountInterestCalculationMethod CalculationMethod { get; set; }
        = AccountInterestCalculationMethod.TieredBalance;

    public InterestPostingFrequency PostingFrequency { get; set; }
        = InterestPostingFrequency.Yearly;

    public InterestDayCountConvention DayCountConvention { get; set; }
        = InterestDayCountConvention.Actual360;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public decimal FlatAnnualRatePercent { get; set; }

    public List<AccountInterestTierEditModel> Tiers { get; set; } = new();

    public static AccountInterestContractEditModel FromContract(
        AccountInterestContract contract)
    {
        var firstTier = contract.Tiers
            .OrderBy(x => x.FromAmount)
            .FirstOrDefault();

        return new AccountInterestContractEditModel
        {
            Id = contract.Id,
            Name = contract.Name,
            CalculationMethod = contract.CalculationMethod,
            PostingFrequency = contract.PostingFrequency,
            DayCountConvention = contract.DayCountConvention,
            StartDate = contract.StartDate,
            EndDate = contract.EndDate,
            IsActive = contract.IsActive,
            FlatAnnualRatePercent = firstTier?.AnnualRatePercent ?? 0m,
            Tiers = contract.Tiers
                .Select(AccountInterestTierEditModel.FromTier)
                .ToList()
        };
    }

    public AccountInterestContract ToContract()
    {
        var tiers = CalculationMethod == AccountInterestCalculationMethod.FlatBalance
            ? new List<AccountInterestTier>
            {
                new AccountInterestTier
                {
                    FromAmount = 0m,
                    ToAmount = null,
                    AnnualRatePercent = FlatAnnualRatePercent
                }
            }
            : Tiers
                .OrderBy(x => x.FromAmount)
                .Select(x => x.ToTier())
                .ToList();

        return new AccountInterestContract
        {
            Id = Id,
            Name = Name.Trim(),
            CalculationMethod = CalculationMethod,
            PostingFrequency = PostingFrequency,
            DayCountConvention = DayCountConvention,
            StartDate = StartDate,
            EndDate = EndDate,
            IsActive = IsActive,
            Tiers = tiers
        };
    }
}