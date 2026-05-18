using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Storage.Json.Tests;

internal static class StorageTestPlanFactory
{
    public static CashFlowPlan CreateSimplePlan()
    {
        var christianId = Guid.Parse("01000000-0000-0000-0000-000000000001");
        var partnerId = Guid.Parse("01000000-0000-0000-0000-000000000002");

        var mainAccountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var savingsAccountId = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var creditCardId = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var mortgageAccountId = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var legacyPillar3aAccountId = Guid.Parse("10000000-0000-0000-0000-000000000005");

        return new CashFlowPlan
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
            Name = "Private Cashflow",
            BaseCurrency = "CHF",

            Persons =
            [
                new Person
                {
                    Id = christianId,
                    DisplayName = "Christian",
                    DateOfBirth = new DateOnly(1985, 1, 15),
                    RetirementDate = new DateOnly(2050, 1, 1),
                    Pillar3aEligibility = Pillar3aEligibilityType.WithPensionFund,
                    AnnualEarnedIncome = 120000m
                },
                new Person
                {
                    Id = partnerId,
                    DisplayName = "Partner",
                    DateOfBirth = new DateOnly(1987, 5, 20),
                    RetirementDate = new DateOnly(2052, 1, 1),
                    Pillar3aEligibility = Pillar3aEligibilityType.WithPensionFund,
                    AnnualEarnedIncome = 90000m
                }
            ],

            Accounts =
            [
                new Account
                {
                    Id = mainAccountId,
                    Name = "Main Account",
                    Type = AccountType.BankAccount,
                    Currency = "CHF",
                    OpeningBalance = 25000m,
                    OpeningDate = new DateOnly(2026, 6, 1),
                    BankName = "Test Bank",
                    Owners =
                    [
                        new AccountOwner
                        {
                            PersonId = christianId,
                            OwnershipShare = 1m
                        }
                    ]
                },
                new Account
                {
                    Id = savingsAccountId,
                    Name = "Savings Account",
                    Type = AccountType.SavingsAccount,
                    Currency = "CHF",
                    OpeningBalance = 80000m,
                    OpeningDate = new DateOnly(2026, 6, 1),
                    Owners =
                    [
                        new AccountOwner
                        {
                            PersonId = christianId,
                            OwnershipShare = 0.5m
                        },
                        new AccountOwner
                        {
                            PersonId = partnerId,
                            OwnershipShare = 0.5m
                        }
                    ],
                    InterestContracts =
                    [
                        new AccountInterestContract
                        {
                            Id = Guid.Parse("50000000-0000-0000-0000-000000000001"),
                            Name = "Savings Interest",
                            CalculationMethod = AccountInterestCalculationMethod.TieredBalance,
                            PostingFrequency = InterestPostingFrequency.Yearly,
                            DayCountConvention = InterestDayCountConvention.Actual360,
                            StartDate = new DateOnly(2026, 1, 1),
                            Tiers =
                            [
                                new AccountInterestTier
                                {
                                    FromAmount = 0m,
                                    ToAmount = 1_000m,
                                    AnnualRatePercent = 2.00m
                                },
                                new AccountInterestTier
                                {
                                    FromAmount = 1_000m,
                                    ToAmount = 100_000m,
                                    AnnualRatePercent = 0.50m
                                },
                                new AccountInterestTier
                                {
                                    FromAmount = 100_000m,
                                    ToAmount = null,
                                    AnnualRatePercent = 0.00m
                                }
                            ],
                            IsActive = true
                        }
                    ]
                },
                new Account
                {
                    Id = creditCardId,
                    Name = "Visa",
                    Type = AccountType.CreditCard,
                    Currency = "CHF",
                    OpeningBalance = -1200m,
                    OpeningDate = new DateOnly(2026, 6, 1),
                    Owners =
                    [
                        new AccountOwner
                        {
                            PersonId = christianId,
                            OwnershipShare = 1m
                        }
                    ]
                },
                new Account
                {
                    Id = mortgageAccountId,
                    Name = "House Mortgage",
                    Type = AccountType.Mortgage,
                    Currency = "CHF",
                    OpeningBalance = -705000m,
                    OpeningDate = new DateOnly(2026, 1, 1),
                    Owners =
                    [
                        new AccountOwner
                        {
                            PersonId = christianId,
                            OwnershipShare = 0.5m
                        },
                        new AccountOwner
                        {
                            PersonId = partnerId,
                            OwnershipShare = 0.5m
                        }
                    ]
                },

                // Temporary legacy model coverage.
                // Keep this until old AccountType.Pillar3a storage/tests/UI are migrated.
                new Account
                {
                    Id = legacyPillar3aAccountId,
                    Name = "Pillar 3a",
                    Type = AccountType.Pillar3a,
                    Pillar3aSubtype = Pillar3aAccountSubtype.FundSolution,
                    Currency = "CHF",
                    OpeningBalance = 50000m,
                    OpeningDate = new DateOnly(2026, 1, 1),
                    Owners =
                    [
                        new AccountOwner
                        {
                            PersonId = christianId,
                            OwnershipShare = 1m
                        }
                    ]
                }
            ],

            Transactions =
            [
                new TransactionDefinition
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000001"),
                    Name = "Salary",
                    Kind = TransactionKind.ExternalIncome,
                    ToAccountId = mainAccountId,
                    IncomePersonId = christianId,
                    Amount = 8500m,
                    Currency = "CHF",
                    Schedule = new Schedule
                    {
                        Frequency = ScheduleFrequency.Monthly,
                        StartDate = new DateOnly(2026, 6, 25),
                        DayOfMonth = 25,
                        BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
                    },
                    Category = "Income",
                    Counterparty = "Employer",
                    PaymentMethod = PaymentMethod.BankTransfer,
                    Priority = 10
                },
                new TransactionDefinition
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000002"),
                    Name = "Car Leasing",
                    Kind = TransactionKind.ExternalExpense,
                    FromAccountId = mainAccountId,
                    Amount = 450m,
                    Currency = "CHF",
                    Schedule = new Schedule
                    {
                        Frequency = ScheduleFrequency.Monthly,
                        StartDate = new DateOnly(2026, 6, 5),
                        EndDate = new DateOnly(2029, 5, 5),
                        DayOfMonth = 5,
                        BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
                    },
                    Category = "Car",
                    Counterparty = "Leasing Company",
                    PaymentMethod = PaymentMethod.Lsv,
                    Priority = 100
                },
                new TransactionDefinition
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000003"),
                    Name = "Savings Transfer",
                    Kind = TransactionKind.InternalTransfer,
                    FromAccountId = mainAccountId,
                    ToAccountId = savingsAccountId,
                    Amount = 1000m,
                    Currency = "CHF",
                    Schedule = new Schedule
                    {
                        Frequency = ScheduleFrequency.Monthly,
                        StartDate = new DateOnly(2026, 6, 26),
                        DayOfMonth = 26,
                        BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
                    },
                    Category = "Saving",
                    PaymentMethod = PaymentMethod.InternalTransfer,
                    Priority = 50
                },

                // Temporary legacy model coverage.
                // New model uses Pillar3aContracts.ContributionSchedules instead.
                new TransactionDefinition
                {
                    Id = Guid.Parse("20000000-0000-0000-0000-000000000004"),
                    Name = "Pillar 3a Contribution",
                    Kind = TransactionKind.InternalTransfer,
                    FromAccountId = mainAccountId,
                    ToAccountId = legacyPillar3aAccountId,
                    Amount = 7258m,
                    Currency = "CHF",
                    Schedule = new Schedule
                    {
                        Frequency = ScheduleFrequency.Once,
                        StartDate = new DateOnly(2026, 12, 20),
                        BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
                    },
                    Category = "Pillar 3a",
                    PaymentMethod = PaymentMethod.InternalTransfer,
                    Priority = 40
                }
            ],

            Mortgages =
            [
                new MortgageContract
                {
                    Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
                    Name = "House SARON Mortgage",
                    Type = MortgageType.Saron,
                    PaymentAccountId = savingsAccountId,
                    InitialPrincipal = 750000m,
                    InitialDate = new DateOnly(2021, 8, 1),
                    CalculationPrincipal = 705000m,
                    CalculationPrincipalDate = new DateOnly(2026, 1, 1),
                    FixedInterestPercent = 0.65m,
                    SaronRates =
                    [
                        new MortgageInterestRatePoint
                        {
                            Date = new DateOnly(2026, 1, 1),
                            RatePercent = 1.20m
                        },
                        new MortgageInterestRatePoint
                        {
                            Date = new DateOnly(2026, 4, 1),
                            RatePercent = 1.50m
                        },
                        new MortgageInterestRatePoint
                        {
                            Date = new DateOnly(2026, 7, 1),
                            RatePercent = 1.40m
                        }
                    ],
                    AmortisationMode = AmortisationMode.Direct,
                    AnnualAmortisationAmount = 9000m,
                    PaymentInterval = MortgagePaymentInterval.Quarterly,
                    BillingCalendar = MortgageBillingCalendar.BankQuarters,
                    IsActive = true
                }
            ],

            CreditCards =
            [
                new CreditCardContract
                {
                    Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
                    Name = "Visa",
                    CreditCardAccountId = creditCardId,
                    PaymentAccountId = mainAccountId,
                    ClosingDayOfMonth = 15,
                    PaymentDayOfMonth = 25,
                    PaymentMethod = CreditCardPaymentMethod.AutomaticLsv,
                    PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
                    StartDate = new DateOnly(2026, 6, 1),
                    IsActive = true
                }
            ],

            Pillar3aContracts =
            [
                new Pillar3aContract
                {
                    Id = Guid.Parse("60000000-0000-0000-0000-000000000001"),
                    Name = "VIAC Global 100",
                    OwnerPersonId = christianId,
                    Type = Pillar3aContractType.Investment,
                    OpeningValue = 50_000m,
                    OpeningDate = new DateOnly(2026, 5, 1),
                    Currency = "CHF",
                    ProviderName = "VIAC",
                    ProjectionAssumption = new Pillar3aProjectionAssumption
                    {
                        Method = Pillar3aProjectionMethod.ExpectedReturn,
                        ExpectedAnnualReturnPercent = 3.0m,
                        AnnualFeePercent = 0.4m
                    },
                    ContributionSchedules =
                    [
                        new Pillar3aContributionSchedule
                        {
                            PaymentAccountId = mainAccountId,
                            StartDate = new DateOnly(2026, 5, 1),
                            EndDate = new DateOnly(2028, 7, 31),
                            Amount = 100m,
                            Currency = "CHF",
                            Frequency = ScheduleFrequency.Monthly,
                            Interval = 1,
                            DayOfMonth = 25,
                            BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
                        },
                        new Pillar3aContributionSchedule
                        {
                            PaymentAccountId = mainAccountId,
                            StartDate = new DateOnly(2028, 8, 1),
                            EndDate = null,
                            Amount = 200m,
                            Currency = "CHF",
                            Frequency = ScheduleFrequency.Monthly,
                            Interval = 1,
                            DayOfMonth = 25,
                            BusinessDayAdjustment = BusinessDayAdjustment.PreviousBusinessDay
                        }
                    ]
                }
            ],

            HouseBuyScenarios =
            [
                new HouseBuySimulatorScenario
                {
                    Name = "Test scenario",
                    SalePrice = 920_000m,
                    SaleRemainingMortgage = 720_000m,
                    BuyPrice = 1_000_000m,
                    DesiredMortgage = 800_000m,
                    Persons =
                    [
                        new HouseBuyScenarioPerson
                        {
                            Name = "Christian",
                            GrossAnnualIncome = 200_000m
                        }
                    ],
                    EquitySources =
                    [
                        new HouseBuyScenarioEquitySource
                        {
                            Name = "Cash",
                            Type = EquitySourceType.Cash,
                            Amount = 150_000m
                        }
                    ]
                }
            ],


            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 6, 1),
                EndDate = new DateOnly(2031, 12, 31),
                Granularity = SimulationGranularity.Daily,
                IncludeInactiveAccounts = false,
                WarnOnNegativeBankBalance = true
            }
        };
    }
}