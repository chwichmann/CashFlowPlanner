using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

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
    public static readonly Guid Pillar3aAccountId = new("10000000-0000-0000-0000-000000000004");

    public static CashFlowPlan CreatePlan(
        IEnumerable<TransactionDefinition>? transactions = null,
        IEnumerable<MortgageContract>? mortgages = null,
        IEnumerable<CreditCardContract>? creditCards = null,
        IEnumerable<Pillar3aContract>? pillar3aContracts = null,
        IEnumerable<RealEstateAsset>? realEstateAssets = null,
        InflationAssumption? inflation = null,
        bool withPillar3aAccount = false)
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

            Accounts = withPillar3aAccount
                ?
                [
                    CreateAccount(MainAccountId, "Main Account", AccountType.BankAccount),
                    CreateAccount(SpareAccountId, "Spare Account", AccountType.SavingsAccount),
                    CreateAccount(CardAccountId, "Visa", AccountType.CreditCard),
                    CreatePillar3aAccount(Pillar3aAccountId, "Pillar 3a Account")
                ]
                :
                [
                    CreateAccount(MainAccountId, "Main Account", AccountType.BankAccount),
                    CreateAccount(SpareAccountId, "Spare Account", AccountType.SavingsAccount),
                    CreateAccount(CardAccountId, "Visa", AccountType.CreditCard)
                ],

            Transactions = transactions?.ToList() ?? [],
            Mortgages = mortgages?.ToList() ?? [],
            CreditCards = creditCards?.ToList() ?? [],
            Pillar3aContracts = pillar3aContracts?.ToList() ?? [],
            RealEstateAssets = realEstateAssets?.ToList() ?? [],
            Inflation = inflation ?? new InflationAssumption(),

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

    /// <summary>
    /// A Pillar 3a account as AccountValidator insists on one: exactly one owner, and a subtype.
    /// </summary>
    public static Account CreatePillar3aAccount(Guid id, string name)
    {
        return new Account
        {
            Id = id,
            Name = name,
            Type = AccountType.Pillar3a,
            Currency = "CHF",
            OpeningBalance = 12000m,
            OpeningDate = new DateOnly(2026, 1, 1),
            Pillar3aSubtype = Pillar3aAccountSubtype.FundSolution,
            Owners =
            [
                new AccountOwner { PersonId = PersonId, OwnershipShare = 1m }
            ]
        };
    }

    public static RealEstateAsset CreateRealEstateAsset(
        Guid? id = null,
        IEnumerable<Guid>? linkedMortgageIds = null)
    {
        return new RealEstateAsset
        {
            Id = id ?? new Guid("20000000-0000-0000-0000-000000000001"),
            Name = "Family Home",
            Type = RealEstateType.House,
            CurrentEstimatedValue = 950_000m,
            ValuationDate = new DateOnly(2026, 1, 1),
            AnnualValueGrowthPercent = 1m,
            Pillar2BvgUsedAmount = 50_000m,
            LinkedMortgageIds = linkedMortgageIds?.ToList() ?? []
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
        Guid? withdrawalTargetAccountId = null,
        Guid? accountId = null,
        Guid? id = null)
    {
        return new Pillar3aContract
        {
            Id = id ?? Guid.NewGuid(),
            Name = "Pillar 3a Fund",
            OwnerPersonId = PersonId,
            AccountId = accountId,
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
