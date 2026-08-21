using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// M14: <see cref="CashFlowPlan.Validate"/> never walked Transactions, Accounts or
/// Persons, and never asserted that Ids are unique. Duplicate account Ids loaded
/// cleanly and then made delete-by-Id throw; dangling mortgage and credit-card
/// account references were written silently into the source-of-truth file.
/// </summary>
public sealed class CashFlowPlanValidationTests
{
    private static readonly Guid BankAccountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid SavingsAccountId = Guid.Parse("10000000-0000-0000-0000-000000000002");
    private static readonly Guid PersonId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid UnknownAccountId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public void Validate_Should_Accept_AConsistentPlan()
    {
        CreatePlan().Validate();
    }

    [Fact]
    public void Validate_Should_Reject_DuplicateAccountIds()
    {
        var plan = CreatePlan();

        plan.Accounts.Add(new Account
        {
            Id = BankAccountId,
            Name = "Duplicate bank account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningDate = new DateOnly(2026, 1, 1)
        });

        AssertThrowsContaining(plan, "duplicate account");
    }

    [Fact]
    public void Validate_Should_Reject_DuplicateTransactionIds()
    {
        var plan = CreatePlan();

        var duplicate = plan.Transactions[0];

        plan.Transactions.Add(new TransactionDefinition
        {
            Id = duplicate.Id,
            Name = "Duplicate transaction",
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = BankAccountId,
            Amount = 10m,
            Currency = "CHF",
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 3, 1))
        });

        AssertThrowsContaining(plan, "duplicate transaction");
    }

    [Fact]
    public void Validate_Should_Reject_DuplicatePersonIds()
    {
        var plan = CreatePlan();

        plan.Persons.Add(new Person
        {
            Id = PersonId,
            DisplayName = "Duplicate person"
        });

        AssertThrowsContaining(plan, "duplicate person");
    }

    [Fact]
    public void Validate_Should_Reject_DuplicateMortgageIds()
    {
        var plan = CreatePlan();

        plan.Mortgages.Add(CreateMortgage(plan.Mortgages[0].Id));

        AssertThrowsContaining(plan, "duplicate mortgage");
    }

    [Fact]
    public void Validate_Should_Reject_DuplicateCreditCardIds()
    {
        var plan = CreatePlan();

        plan.CreditCards.Add(CreateCreditCard(plan.CreditCards[0].Id));

        AssertThrowsContaining(plan, "duplicate credit card");
    }

    [Fact]
    public void Validate_Should_Reject_DuplicatePillar3aIds()
    {
        var plan = CreatePlan();

        plan.Pillar3aContracts.Add(CreatePillar3aContract(plan.Pillar3aContracts[0].Id));

        AssertThrowsContaining(plan, "duplicate pillar 3a");
    }

    [Fact]
    public void Validate_Should_WalkTransactions()
    {
        var plan = CreatePlan();

        // Amount <= 0 is rejected by TransactionDefinition.Validate, which the plan
        // never called. Inactive transactions were not validated by anyone at all.
        plan.Transactions.Add(new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Broken",
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = BankAccountId,
            Amount = 0m,
            Currency = "CHF",
            IsActive = false,
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 3, 1))
        });

        AssertThrowsContaining(plan, "positive amount");
    }

    [Fact]
    public void Validate_Should_Reject_TransactionWithUnknownFromAccount()
    {
        var plan = CreatePlan();

        plan.Transactions.Add(TestPlanBuilder.ExternalExpense(
            fromAccountId: UnknownAccountId,
            amount: 100m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 3, 1)),
            name: "Dangling expense"));

        AssertThrowsContaining(plan, "unknown");
    }

    [Fact]
    public void Validate_Should_Reject_TransactionWithUnknownToAccount()
    {
        var plan = CreatePlan();

        plan.Transactions.Add(TestPlanBuilder.ExternalIncome(
            toAccountId: UnknownAccountId,
            amount: 100m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 3, 1)),
            name: "Dangling income"));

        AssertThrowsContaining(plan, "unknown");
    }

    [Fact]
    public void Validate_Should_Reject_MortgageWithUnknownPaymentAccount()
    {
        var plan = CreatePlan();

        plan.Mortgages[0] = CreateMortgage(
            Guid.NewGuid(),
            paymentAccountId: UnknownAccountId);

        AssertThrowsContaining(plan, "unknown");
    }

    [Fact]
    public void Validate_Should_Reject_MortgageWithUnknownIndirectAmortisationAccount()
    {
        var plan = CreatePlan();

        plan.Mortgages[0] = CreateMortgage(
            Guid.NewGuid(),
            indirectAmortisationAccountId: UnknownAccountId);

        AssertThrowsContaining(plan, "unknown");
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public void Validate_Should_Reject_CreditCardWithUnknownAccount(
        bool danglingCardAccount,
        bool danglingPaymentAccount)
    {
        var plan = CreatePlan();

        plan.CreditCards[0] = CreateCreditCard(
            Guid.NewGuid(),
            creditCardAccountId: danglingCardAccount ? UnknownAccountId : SavingsAccountId,
            paymentAccountId: danglingPaymentAccount ? UnknownAccountId : BankAccountId);

        AssertThrowsContaining(plan, "unknown");
    }

    [Fact]
    public void Validate_Should_Reject_AccountOwnedByUnknownPerson()
    {
        var plan = CreatePlan();

        plan.Accounts[0].Owners =
        [
            new AccountOwner
            {
                PersonId = Guid.NewGuid(),
                OwnershipShare = 1m
            }
        ];

        AssertThrowsContaining(plan, "person");
    }

    [Fact]
    public void Validate_Should_Reject_Pillar3aAccountWithoutSubtype()
    {
        var plan = CreatePlan();

        plan.Accounts.Add(new Account
        {
            Id = Guid.NewGuid(),
            Name = "VIAC",
            Type = AccountType.Pillar3a,
            Currency = "CHF",
            OpeningDate = new DateOnly(2026, 1, 1),
            Owners =
            [
                new AccountOwner
                {
                    PersonId = PersonId,
                    OwnershipShare = 1m
                }
            ]
        });

        AssertThrowsContaining(plan, "pillar 3a");
    }

    private static void AssertThrowsContaining(CashFlowPlan plan, string expectedFragment)
    {
        var exception = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains(
            expectedFragment,
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }

    private static CashFlowPlan CreatePlan()
    {
        var bankAccount = new Account
        {
            Id = BankAccountId,
            Name = "Bank Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 10_000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var savingsAccount = new Account
        {
            Id = SavingsAccountId,
            Name = "Savings Account",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 20_000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var person = new Person
        {
            Id = PersonId,
            DisplayName = "Christian",
            RetirementDate = new DateOnly(2050, 1, 1)
        };

        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Validation test plan",
            BaseCurrency = "CHF",
            Persons = [person],
            Accounts = [bankAccount, savingsAccount],
            Transactions =
            [
                TestPlanBuilder.ExternalIncome(
                    toAccountId: BankAccountId,
                    amount: 5_000m,
                    schedule: TestPlanBuilder.Once(new DateOnly(2026, 2, 1)),
                    name: "Salary")
            ],
            Mortgages = [CreateMortgage(Guid.NewGuid())],
            CreditCards = [CreateCreditCard(Guid.NewGuid())],
            Pillar3aContracts = [CreatePillar3aContract(Guid.NewGuid())],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }

    private static MortgageContract CreateMortgage(
        Guid id,
        Guid? paymentAccountId = null,
        Guid? indirectAmortisationAccountId = null)
    {
        return new MortgageContract
        {
            Id = id,
            Name = "House Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = paymentAccountId ?? BankAccountId,
            IndirectAmortisationAccountId = indirectAmortisationAccountId,
            InitialPrincipal = 700_000m,
            InitialDate = new DateOnly(2021, 8, 1),
            FixedInterestPercent = 1.2m,
            AmortisationMode = AmortisationMode.None,
            IsActive = true
        };
    }

    private static CreditCardContract CreateCreditCard(
        Guid id,
        Guid? creditCardAccountId = null,
        Guid? paymentAccountId = null)
    {
        return new CreditCardContract
        {
            Id = id,
            Name = "Visa",
            CreditCardAccountId = creditCardAccountId ?? SavingsAccountId,
            PaymentAccountId = paymentAccountId ?? BankAccountId,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }

    private static Pillar3aContract CreatePillar3aContract(Guid id)
    {
        return new Pillar3aContract
        {
            Id = id,
            Name = "VIAC",
            OwnerPersonId = PersonId,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF"
        };
    }
}
