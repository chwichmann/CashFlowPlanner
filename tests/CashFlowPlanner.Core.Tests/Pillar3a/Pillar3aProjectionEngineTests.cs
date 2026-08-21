using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests.Pillar3a;

/// <summary>
/// Real coverage for <see cref="Pillar3aProjectionEngine"/>.
///
/// This file previously contained zero tests: it was a stale verbatim copy of the
/// production class, in the production namespace, so it shadowed the real type
/// inside the test assembly while looking like a test suite.
/// </summary>
public sealed class Pillar3aProjectionEngineTests
{
    private static readonly Guid PaymentAccountId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid PersonId = Guid.Parse("20000000-0000-0000-0000-000000000001");

    [Theory]
    // 12'000 over January (31 days) at 3% net:
    // 12'000 * 3% * 31 / 365 = 30.575... => 30.58
    [InlineData(Pillar3aProjectionMethod.FixedInterest, 3, 0, 30.58)]
    // FixedInterest deliberately ignores the annual fee.
    [InlineData(Pillar3aProjectionMethod.FixedInterest, 3, 1, 30.58)]
    // ExpectedReturn nets the fee off the return.
    [InlineData(Pillar3aProjectionMethod.ExpectedReturn, 3, 0, 30.58)]
    [InlineData(Pillar3aProjectionMethod.ExpectedReturn, 4, 1, 30.58)]
    // These two methods never grow the value.
    [InlineData(Pillar3aProjectionMethod.None, 5, 0, 0)]
    [InlineData(Pillar3aProjectionMethod.InsuranceGuaranteedPayout, 5, 0, 0)]
    public void Project_Growth_FollowsTheProjectionMethod(
        Pillar3aProjectionMethod method,
        decimal expectedAnnualReturnPercent,
        decimal annualFeePercent,
        decimal expectedGrowth)
    {
        var contract = CreateContract(
            openingValue: 12_000m,
            assumption: new Pillar3aProjectionAssumption
            {
                Method = method,
                ExpectedAnnualReturnPercent = expectedAnnualReturnPercent,
                AnnualFeePercent = annualFeePercent
            });

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 1, 31));

        var point = Assert.Single(points);

        Assert.Equal(new DateOnly(2026, 1, 31), point.Date);
        Assert.Equal(expectedGrowth, point.Growth);
        Assert.Equal(12_000m + expectedGrowth, point.Value);
    }

    [Fact]
    public void Project_Growth_IsProRatedFromTheProjectionStart_NotTheMonthStart()
    {
        // The contract only opens on 15 June, so June must earn 16 days of growth,
        // not 30.
        // 12'000 * 3% * 16 / 365 = 15.780... => 15.78
        var contract = CreateContract(
            openingValue: 12_000m,
            openingDate: new DateOnly(2026, 6, 15),
            assumption: new Pillar3aProjectionAssumption
            {
                Method = Pillar3aProjectionMethod.FixedInterest,
                ExpectedAnnualReturnPercent = 3m
            });

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 6, 30));

        var point = Assert.Single(points);

        Assert.Equal(new DateOnly(2026, 6, 30), point.Date);
        Assert.Equal(15.78m, point.Growth);
    }

    [Fact]
    public void Project_Growth_IsCalculatedOnTheValueBeforeThisMonthsContribution()
    {
        var contract = CreateContract(
            openingValue: 0m,
            assumption: new Pillar3aProjectionAssumption
            {
                Method = Pillar3aProjectionMethod.ExpectedReturn,
                ExpectedAnnualReturnPercent = 12m
            },
            contributionSchedules:
            [
                new Pillar3aContributionSchedule
                {
                    Id = Guid.NewGuid(),
                    PaymentAccountId = PaymentAccountId,
                    StartDate = new DateOnly(2026, 1, 1),
                    Amount = 1_000m,
                    Currency = "CHF",
                    Frequency = ScheduleFrequency.Monthly,
                    Interval = 1,
                    DayOfMonth = 10,
                    IsActive = true
                }
            ]);

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 3, 31));

        Assert.Equal(3, points.Count);

        // January starts at zero, so the January contribution earns nothing yet.
        Assert.Equal(1_000m, points[0].Contributions);
        Assert.Equal(0m, points[0].Growth);
        Assert.Equal(1_000m, points[0].Value);

        // February grows the 1'000 already there, for 28 days:
        // 1'000 * 12% * 28 / 365 = 9.205... => 9.21
        Assert.Equal(1_000m, points[1].Contributions);
        Assert.Equal(9.21m, points[1].Growth);
        Assert.Equal(2_009.21m, points[1].Value);
    }

    [Fact]
    public void Project_Withdrawal_ReducesTheValue_AndIsReportedOnItsMonth()
    {
        var contract = CreateContract(
            openingValue: 10_000m,
            assumption: NoGrowth(),
            withdrawals:
            [
                new Pillar3aWithdrawalEvent
                {
                    Id = Guid.NewGuid(),
                    Date = new DateOnly(2026, 2, 15),
                    Reason = Pillar3aWithdrawalReason.OwnerOccupiedHome,
                    Amount = 3_000m
                }
            ]);

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 3, 31));

        Assert.Equal(3, points.Count);

        Assert.Equal(0m, points[0].Withdrawals);
        Assert.Equal(10_000m, points[0].Value);

        Assert.Equal(3_000m, points[1].Withdrawals);
        Assert.Equal(7_000m, points[1].Value);

        Assert.Equal(0m, points[2].Withdrawals);
        Assert.Equal(7_000m, points[2].Value);
    }

    [Fact]
    public void Project_Withdrawal_LargerThanTheValue_FloorsTheValueAtZero()
    {
        var contract = CreateContract(
            openingValue: 10_000m,
            assumption: NoGrowth(),
            withdrawals:
            [
                new Pillar3aWithdrawalEvent
                {
                    Id = Guid.NewGuid(),
                    Date = new DateOnly(2026, 2, 15),
                    Reason = Pillar3aWithdrawalReason.OwnerOccupiedHome,
                    Amount = 25_000m
                }
            ]);

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 3, 31));

        Assert.Equal(0m, points[1].Value);
        Assert.Equal(0m, points[2].Value);
    }

    [Fact]
    public void Project_ClosingWithdrawal_PaysOutTheWholeValue_AndEndsTheProjection()
    {
        var contract = CreateContract(
            openingValue: 10_000m,
            assumption: NoGrowth(),
            withdrawals:
            [
                new Pillar3aWithdrawalEvent
                {
                    Id = Guid.NewGuid(),
                    Date = new DateOnly(2026, 2, 15),
                    Reason = Pillar3aWithdrawalReason.Retirement,
                    CloseContract = true
                }
            ]);

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 1, 1),
            retirementDate: new DateOnly(2026, 12, 31));

        // The projection stops in the month the contract closes.
        Assert.Equal(2, points.Count);

        Assert.Equal(new DateOnly(2026, 2, 28), points[1].Date);
        Assert.Equal(10_000m, points[1].Withdrawals);
        Assert.Equal(0m, points[1].Value);
    }

    [Fact]
    public void Project_Should_ReturnNoPoints_WhenRetirementIsAlreadyInThePast()
    {
        var contract = CreateContract(
            openingValue: 10_000m,
            assumption: NoGrowth());

        var points = ProjectSingleContract(
            contract,
            today: new DateOnly(2026, 6, 1),
            retirementDate: new DateOnly(2026, 5, 1));

        Assert.Empty(points);
    }

    [Fact]
    public void Project_Should_SkipInactiveContracts()
    {
        var contract = CreateContract(
            openingValue: 10_000m,
            assumption: NoGrowth(),
            isActive: false);

        var plan = CreatePlan(contract, new DateOnly(2026, 12, 31));

        var results = new Pillar3aProjectionEngine()
            .Project(plan, new DateOnly(2026, 1, 1));

        Assert.Empty(results);
    }

    private static Pillar3aProjectionAssumption NoGrowth()
    {
        return new Pillar3aProjectionAssumption
        {
            Method = Pillar3aProjectionMethod.None
        };
    }

    private static IReadOnlyList<Pillar3aProjectionPoint> ProjectSingleContract(
        Pillar3aContract contract,
        DateOnly today,
        DateOnly retirementDate)
    {
        var plan = CreatePlan(contract, retirementDate);

        var results = new Pillar3aProjectionEngine().Project(plan, today);

        var result = Assert.Single(results);

        Assert.Equal(contract.Id, result.ContractId);

        return result.Points;
    }

    private static CashFlowPlan CreatePlan(
        Pillar3aContract contract,
        DateOnly retirementDate)
    {
        var paymentAccount = new Account
        {
            Id = PaymentAccountId,
            Name = "Bank Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 100_000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var person = new Person
        {
            Id = PersonId,
            DisplayName = "Christian",
            RetirementDate = retirementDate
        };

        return TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [paymentAccount],
            pillar3aContracts: [contract]);
    }

    private static Pillar3aContract CreateContract(
        decimal openingValue,
        Pillar3aProjectionAssumption assumption,
        DateOnly? openingDate = null,
        List<Pillar3aContributionSchedule>? contributionSchedules = null,
        List<Pillar3aWithdrawalEvent>? withdrawals = null,
        bool isActive = true)
    {
        return new Pillar3aContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "VIAC",
            OwnerPersonId = PersonId,
            Type = Pillar3aContractType.Investment,
            OpeningValue = openingValue,
            OpeningDate = openingDate ?? new DateOnly(2026, 1, 1),
            Currency = "CHF",
            IsActive = isActive,
            ProjectionAssumption = assumption,
            ContributionSchedules = contributionSchedules ?? [],
            Withdrawals = withdrawals ?? []
        };
    }
}
