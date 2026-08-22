using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.Tests.Validation;

/// <summary>
/// Referential integrity for the two links added in wave 4: a property to its
/// mortgages, and a Pillar 3a contract to the account its balance lives in.
/// </summary>
public sealed class RealEstateAndPillar3aLinkValidationTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);

    [Fact]
    public void Validate_RealEstateAssetLinkingAKnownMortgage_Passes()
    {
        var mortgage = CreateMortgage(TestPlanBuilder.CreateBankAccount().Id);

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [new Account { Id = mortgage.PaymentAccountId, Name = "Bank", OpeningDate = Start }],
            mortgages: [mortgage],
            realEstateAssets: [CreateAsset("Flat", mortgage.Id)]);

        plan.Validate();
    }

    [Fact]
    public void Validate_RealEstateAssetLinkingAnUnknownMortgage_Throws()
    {
        var plan = TestPlanBuilder.CreatePlan(
            realEstateAssets: [CreateAsset("Flat", Guid.NewGuid())]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("references unknown mortgage", error.Message);
    }

    /// <summary>
    /// The same debt netted off two properties would show up twice on the asset
    /// side and once on the liability side.
    /// </summary>
    [Fact]
    public void Validate_OneMortgageLinkedToTwoProperties_Throws()
    {
        var bank = TestPlanBuilder.CreateBankAccount();
        var mortgage = CreateMortgage(bank.Id);

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bank],
            mortgages: [mortgage],
            realEstateAssets:
            [
                CreateAsset("Flat", mortgage.Id),
                CreateAsset("House", mortgage.Id)
            ]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("linked to more than one real estate asset", error.Message);
    }

    [Fact]
    public void Validate_DuplicateRealEstateAssetIds_Throws()
    {
        var id = Guid.NewGuid();

        var plan = TestPlanBuilder.CreatePlan(
            realEstateAssets:
            [
                new RealEstateAsset { Id = id, Name = "Flat", CurrentEstimatedValue = 1m },
                new RealEstateAsset { Id = id, Name = "House", CurrentEstimatedValue = 1m }
            ]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("duplicate real estate asset ids", error.Message);
    }

    [Theory]
    [InlineData(-1, 0, "negative estimated value")]
    [InlineData(0, -1, "negative Pillar 2 (BVG) withdrawal amount")]
    public void Validate_RealEstateAssetWithNegativeAmounts_Throws(
        int value,
        int bvg,
        string expectedFragment)
    {
        var plan = TestPlanBuilder.CreatePlan(
            realEstateAssets:
            [
                new RealEstateAsset
                {
                    Name = "Flat",
                    CurrentEstimatedValue = value,
                    Pillar2BvgUsedAmount = bvg
                }
            ]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains(expectedFragment, error.Message);
    }

    [Fact]
    public void Validate_GrowthAssumptionWithoutValuationDate_Throws()
    {
        var plan = TestPlanBuilder.CreatePlan(
            realEstateAssets:
            [
                new RealEstateAsset
                {
                    Name = "Flat",
                    CurrentEstimatedValue = 900_000m,
                    AnnualValueGrowthPercent = 2m
                }
            ]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("states no valuation date", error.Message);
    }

    [Fact]
    public void Validate_Pillar3aContractLinkedToAPillar3aAccount_Passes()
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };
        var payment = TestPlanBuilder.CreateBankAccount();
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(ownerPersonId: person.Id);

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [payment, pillar3aAccount],
            pillar3aContracts: [CreateContract(person.Id, payment.Id, pillar3aAccount.Id)]);

        plan.Validate();
    }

    [Fact]
    public void Validate_Pillar3aContractLinkedToAnUnknownAccount_Throws()
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };
        var payment = TestPlanBuilder.CreateBankAccount();

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [payment],
            pillar3aContracts: [CreateContract(person.Id, payment.Id, Guid.NewGuid())]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("references unknown account", error.Message);
    }

    [Theory]
    [InlineData(AccountType.BankAccount)]
    [InlineData(AccountType.SavingsAccount)]
    [InlineData(AccountType.Investment)]
    [InlineData(AccountType.CreditCard)]
    public void Validate_Pillar3aContractLinkedToTheWrongAccountType_Throws(AccountType type)
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };
        var payment = TestPlanBuilder.CreateBankAccount();

        var wrong = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Not a Pillar 3a account",
            Type = type,
            Currency = "CHF",
            OpeningDate = Start
        };

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [payment, wrong],
            pillar3aContracts: [CreateContract(person.Id, payment.Id, wrong.Id)]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("must be linked to a Pillar 3a account", error.Message);
    }

    /// <summary>
    /// Two contracts on one account would count that balance twice in the
    /// net-worth series.
    /// </summary>
    [Fact]
    public void Validate_TwoContractsSharingOnePillar3aAccount_Throws()
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };
        var payment = TestPlanBuilder.CreateBankAccount();
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(ownerPersonId: person.Id);

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [payment, pillar3aAccount],
            pillar3aContracts:
            [
                CreateContract(person.Id, payment.Id, pillar3aAccount.Id, "VIAC"),
                CreateContract(person.Id, payment.Id, pillar3aAccount.Id, "finpension")
            ]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("linked to more than one contract", error.Message);
    }

    private static RealEstateAsset CreateAsset(string name, Guid mortgageId)
    {
        return new RealEstateAsset
        {
            Id = Guid.NewGuid(),
            Name = name,
            CurrentEstimatedValue = 900_000m,
            LinkedMortgageIds = [mortgageId]
        };
    }

    private static MortgageContract CreateMortgage(Guid paymentAccountId)
    {
        return new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage",
            PaymentAccountId = paymentAccountId,
            InitialPrincipal = 700_000m,
            InitialDate = Start,
            FixedInterestPercent = 1m
        };
    }

    private static Pillar3aContract CreateContract(
        Guid personId,
        Guid paymentAccountId,
        Guid? accountId,
        string name = "VIAC")
    {
        return new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = name,
            OwnerPersonId = personId,
            AccountId = accountId,
            OpeningDate = Start,
            Currency = "CHF",
            ProjectionAssumption = new Pillar3aProjectionAssumption
            {
                Method = Pillar3aProjectionMethod.None
            },
            ContributionSchedules =
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = paymentAccountId,
                    StartDate = Start,
                    Amount = 600m,
                    Currency = "CHF",
                    Frequency = ScheduleFrequency.Monthly,
                    Interval = 1,
                    DayOfMonth = 10
                }
            ]
        };
    }
}
