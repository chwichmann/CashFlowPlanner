using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests.Pillar3a;

public sealed class Pillar3aTaxYearSimulatorTests
{
    [Fact]
    public void Simulate_MonthlyScheduleWithinTaxYear_ReturnsAnnualContribution()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian"
        };

        var paymentAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var contract = new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "VIAC",
            OwnerPersonId = person.Id,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = paymentAccount.Id,
                    StartDate = new DateOnly(2026, 1, 1),
                    Amount = 100m,
                    Currency = "CHF",
                    Frequency = ScheduleFrequency.Monthly,
                    Interval = 1,
                    DayOfMonth = 10,
                    IsActive = true
                }
            ]
        };

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [paymentAccount],
            pillar3aContracts: [contract],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var simulator = new Pillar3aTaxYearSimulator();

        var result = simulator.Simulate(
            plan,
            2026,
            7_258m);

        var personResult = Assert.Single(result.Persons);

        Assert.Equal(person.Id, personResult.PersonId);
        Assert.Equal(1_200m, personResult.ScheduledContributions);
        Assert.Equal(6_058m, personResult.Remaining);
        Assert.Equal(0m, personResult.Excess);
        Assert.False(personResult.IsLimitReached);
        Assert.False(personResult.IsExceeded);
    }

    [Fact]
    public void Simulate_ContributionsAboveLimit_ReturnsExcess()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian"
        };

        var paymentAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 20_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var contract = new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "VIAC",
            OwnerPersonId = person.Id,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = paymentAccount.Id,
                    StartDate = new DateOnly(2026, 1, 1),
                    Amount = 800m,
                    Currency = "CHF",
                    Frequency = ScheduleFrequency.Monthly,
                    Interval = 1,
                    DayOfMonth = 10,
                    IsActive = true
                }
            ]
        };

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [paymentAccount],
            pillar3aContracts: [contract],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var simulator = new Pillar3aTaxYearSimulator();

        var result = simulator.Simulate(
            plan,
            2026,
            7_258m);

        var personResult = Assert.Single(result.Persons);

        Assert.Equal(9_600m, personResult.ScheduledContributions);
        Assert.Equal(0m, personResult.Remaining);
        Assert.Equal(2_342m, personResult.Excess);
        Assert.True(personResult.IsLimitReached);
        Assert.True(personResult.IsExceeded);
    }

    [Fact]
    public void Simulate_TwoContractsForSamePerson_AreSummed()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian"
        };

        var paymentAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 20_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var firstContract = CreateContract(
            "VIAC 1",
            person.Id,
            paymentAccount.Id,
            100m);

        var secondContract = CreateContract(
            "VIAC 2",
            person.Id,
            paymentAccount.Id,
            200m);

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [paymentAccount],
            pillar3aContracts: [firstContract, secondContract],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var simulator = new Pillar3aTaxYearSimulator();

        var result = simulator.Simulate(
            plan,
            2026,
            7_258m);

        var personResult = Assert.Single(result.Persons);

        Assert.Equal(3_600m, personResult.ScheduledContributions);
        Assert.Equal(3_658m, personResult.Remaining);
        Assert.Equal(0m, personResult.Excess);
    }

    private static Pillar3aContract CreateContract(
        string name,
        Guid personId,
        Guid paymentAccountId,
        decimal monthlyAmount)
    {
        return new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerPersonId = personId,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = paymentAccountId,
                    StartDate = new DateOnly(2026, 1, 1),
                    Amount = monthlyAmount,
                    Currency = "CHF",
                    Frequency = ScheduleFrequency.Monthly,
                    Interval = 1,
                    DayOfMonth = 10,
                    IsActive = true
                }
            ]
        };
    }
}
