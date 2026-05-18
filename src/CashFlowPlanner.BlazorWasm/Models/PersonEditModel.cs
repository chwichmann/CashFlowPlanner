using CashFlowPlanner.Core.People;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class PersonEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string DisplayName { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    public DateOnly? RetirementDate { get; set; }

    public Pillar3aEligibilityType Pillar3aEligibility { get; set; }
        = Pillar3aEligibilityType.WithPensionFund;

    public decimal? AnnualEarnedIncome { get; set; }

    public static PersonEditModel FromPerson(Person person)
    {
        return new PersonEditModel
        {
            Id = person.Id,
            DisplayName = person.DisplayName,
            DateOfBirth = person.DateOfBirth,
            RetirementDate = person.RetirementDate,
            Pillar3aEligibility = person.Pillar3aEligibility,
            AnnualEarnedIncome = person.AnnualEarnedIncome
        };
    }

    public Person ToPerson()
    {
        return new Person
        {
            Id = Id,
            DisplayName = DisplayName.Trim(),
            DateOfBirth = DateOfBirth,
            RetirementDate = RetirementDate,
            Pillar3aEligibility = Pillar3aEligibility,
            AnnualEarnedIncome = AnnualEarnedIncome
        };
    }
}