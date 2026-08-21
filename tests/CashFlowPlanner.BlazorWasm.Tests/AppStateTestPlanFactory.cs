using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Small hand-built plans for state tests. Ids are fixed so failures are readable.
/// </summary>
internal static class AppStateTestPlanFactory
{
    public static readonly Guid PlanId = new("00000000-0000-0000-0000-0000000000aa");
    public static readonly Guid PersonId = new("01000000-0000-0000-0000-000000000001");
    public static readonly Guid MainAccountId = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid SpareAccountId = new("10000000-0000-0000-0000-000000000002");
    public static readonly Guid CardAccountId = new("10000000-0000-0000-0000-000000000003");

    public static CashFlowPlan CreatePlan(
        IEnumerable<TransactionDefinition>? transactions = null,
        IEnumerable<MortgageContract>? mortgages = null,
        IEnumerable<CreditCardContract>? creditCards = null,
        IEnumerable<Pillar3aContract>? pillar3aContracts = null)
    {
        return new CashFlowPlan
        {
            Id = PlanId,
            Name = "Test Plan",
            BaseCurrency = "CHF",

            Persons =
            [
                new Person
                {
                    Id = PersonId,
                    DisplayName = "Christian",
                    DateOfBirth = new DateOnly(1985, 5, 5)
                }
            ],

            Accounts =
            [
                CreateAccount(MainAccountId, "Main Account", AccountType.BankAccount),
                CreateAccount(SpareAccountId, "Spare Account", AccountType.SavingsAccount),
                CreateAccount(CardAccountId, "Visa", AccountType.CreditCard)
            ],

            Transactions = transactions?.ToList() ?? [],
            Mortgages = mortgages?.ToList() ?? [],
            CreditCards = creditCards?.ToList() ?? [],
            Pillar3aContracts = pillar3aContracts?.ToList() ?? [],

            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }

    public static Account CreateAccount(Guid id, string name, AccountType type)
    {
        return new Account
        {
            Id = id,
            Name = name,
            Type = type,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };
    }

    public static TransactionDefinition CreateTransaction(Guid? from, Guid? to)
    {
        return new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Rent",
            Kind = to is null
                ? TransactionKind.ExternalExpense
                : TransactionKind.InternalTransfer,
            FromAccountId = from,
            ToAccountId = to,
            Amount = 100m,
            Currency = "CHF",
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 1),
                DayOfMonth = 1
            }
        };
    }

    public static MortgageContract CreateMortgage(
        Guid paymentAccountId,
        Guid? indirectAmortisationAccountId = null)
    {
        return new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "House Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = paymentAccountId,
            IndirectAmortisationAccountId = indirectAmortisationAccountId,
            AmortisationMode = indirectAmortisationAccountId is null
                ? AmortisationMode.Direct
                : AmortisationMode.Indirect,
            AnnualAmortisationAmount = 9000m,
            InitialPrincipal = 500000m,
            InitialDate = new DateOnly(2025, 1, 1),
            FixedInterestPercent = 1.2m,
            PaymentInterval = MortgagePaymentInterval.Quarterly
        };
    }

    public static CreditCardContract CreateCreditCard(
        Guid creditCardAccountId,
        Guid paymentAccountId)
    {
        return new CreditCardContract
        {
            Id = Guid.NewGuid(),
            Name = "Visa Card",
            CreditCardAccountId = creditCardAccountId,
            PaymentAccountId = paymentAccountId,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            StartDate = new DateOnly(2026, 1, 1)
        };
    }

    public static Pillar3aContract CreatePillar3aContract(
        Guid? contributionAccountId = null,
        Guid? withdrawalTargetAccountId = null)
    {
        return new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "Pillar 3a Fund",
            OwnerPersonId = PersonId,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 10000m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Currency = "CHF",

            ContributionSchedules = contributionAccountId is null
                ? []
                :
                [
                    new Pillar3aContributionSchedule
                    {
                        PaymentAccountId = contributionAccountId.Value,
                        StartDate = new DateOnly(2026, 1, 1),
                        Amount = 600m,
                        Currency = "CHF",
                        Frequency = ScheduleFrequency.Monthly,
                        DayOfMonth = 1
                    }
                ],

            Withdrawals = withdrawalTargetAccountId is null
                ? []
                :
                [
                    new Pillar3aWithdrawalEvent
                    {
                        Date = new DateOnly(2026, 6, 1),
                        Amount = 1000m,
                        TargetAccountId = withdrawalTargetAccountId.Value
                    }
                ]
        };
    }
}
