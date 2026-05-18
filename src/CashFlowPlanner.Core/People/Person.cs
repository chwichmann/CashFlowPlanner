namespace CashFlowPlanner.Core.People;

public sealed class Person
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly? RetirementDate { get; set; }

    public Pillar3aEligibilityType Pillar3aEligibility { get; set; }
        = Pillar3aEligibilityType.WithPensionFund;

    public decimal? AnnualEarnedIncome { get; set; }
}