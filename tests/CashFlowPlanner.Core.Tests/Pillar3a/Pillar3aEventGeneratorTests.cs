using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core.Tests.Pillar3a;

/// <summary>
/// Finding H8 and the never-simulated withdrawals.
/// </summary>
public sealed class Pillar3aEventGeneratorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    [Fact]
    public void Contribution_LinkedContract_IsAnInternalTransferToThePillar3aAccount()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount();

        var contract = CreateContract(
            payment.Id,
            accountId: pillar3aAccount.Id,
            amount: 600m);

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        Assert.Equal(12, result.Events.Count);

        Assert.All(result.Events, e =>
        {
            Assert.Equal(TransactionKind.InternalTransfer, e.Kind);
            Assert.Equal(payment.Id, e.FromAccountId);
            Assert.Equal(pillar3aAccount.Id, e.ToAccountId);
        });

        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// The measurable form of H8: run a year of contributions through the full
    /// engine and check the money is still in the plan afterwards.
    /// </summary>
    [Fact]
    public void Simulate_LinkedContract_MovesContributionsInsteadOfDestroyingThem()
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(ownerPersonId: person.Id);

        var plan = TestPlanBuilder.CreatePlan(
            persons: [person],
            accounts: [payment, pillar3aAccount],
            pillar3aContracts:
            [
                CreateContract(payment.Id, pillar3aAccount.Id, 600m, person.Id)
            ],
            startDate: Start,
            endDate: End);

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(20_000m - 7_200m, result.GetBalance(payment.Id, End));
        Assert.Equal(7_200m, result.GetBalance(pillar3aAccount.Id, End));

        var netWorth = result.TryGetNetWorth(End);

        Assert.NotNull(netWorth);
        Assert.Equal(20_000m, netWorth.NetWorth);
        Assert.Equal(7_200m, netWorth.Pillar3aAssets);
        Assert.Equal(12_800m, netWorth.LiquidAssets);
    }

    /// <summary>
    /// An unlinked contract keeps the old behaviour -- there is no account to
    /// credit -- but it no longer does so silently.
    /// </summary>
    [Fact]
    public void Contribution_UnlinkedContract_StaysAnExpenseAndWarns()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);

        var result = new Pillar3aEventGenerator().Generate(
            [CreateContract(payment.Id, accountId: null, amount: 600m)],
            [payment],
            Start,
            End);

        Assert.All(result.Events, e =>
        {
            Assert.Equal(TransactionKind.ExternalExpense, e.Kind);
            Assert.Null(e.ToAccountId);
        });

        var warning = Assert.Single(result.Warnings);

        Assert.Equal("PILLAR3A_CONTRACT_NOT_LINKED", warning.Code);
    }

    [Fact]
    public void Withdrawal_WithExplicitAmount_TransfersToTheTargetAccount()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(openingBalance: 100_000m);

        var contract = CreateContract(payment.Id, pillar3aAccount.Id, amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 30),
            Amount = 40_000m,
            TargetAccountId = payment.Id,
            Reason = Pillar3aWithdrawalReason.Retirement
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        var withdrawal = Assert.Single(
            result.Events,
            x => x.Category == "Pillar 3a Withdrawal");

        Assert.Equal(TransactionKind.InternalTransfer, withdrawal.Kind);
        Assert.Equal(pillar3aAccount.Id, withdrawal.FromAccountId);
        Assert.Equal(payment.Id, withdrawal.ToAccountId);
        Assert.Equal(40_000m, withdrawal.Amount);

        // A partial withdrawal does not stop the contributions.
        Assert.Equal(12, result.Events.Count(x => x.Category == "Pillar 3a Contribution"));
    }

    /// <summary>
    /// A retirement payout is the single largest event in a Pillar 3a contract's
    /// life and it never reached the cash flow at all before this.
    /// </summary>
    [Fact]
    public void Withdrawal_ClosingWithNoAmount_SweepsTheBalanceAndStopsContributions()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(openingBalance: 100_000m);

        var contract = CreateContract(payment.Id, pillar3aAccount.Id, amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 30),
            CloseContract = true,
            TargetAccountId = payment.Id
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        var contributions = result.Events
            .Where(x => x.Category == "Pillar 3a Contribution")
            .ToList();

        // January to June inclusive, then nothing: the contract is closed.
        Assert.Equal(6, contributions.Count);
        Assert.All(contributions, e => Assert.True(e.Date <= new DateOnly(2026, 6, 30)));

        var withdrawal = Assert.Single(
            result.Events,
            x => x.Category == "Pillar 3a Withdrawal");

        Assert.Equal(100_000m + (6 * 600m), withdrawal.Amount);
        Assert.Empty(result.Warnings);
    }

    /// <summary>
    /// A closing sweep is computed from contributions, which the generator can
    /// see, and not from account interest, which is generated afterwards. When
    /// the account bears interest the approximation is declared.
    /// </summary>
    [Fact]
    public void Withdrawal_ClosingAnInterestBearingAccount_WarnsThatGrowthIsExcluded()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(openingBalance: 100_000m);

        pillar3aAccount.InterestContracts.Add(new AccountInterestContract
        {
            Id = Guid.NewGuid(),
            Name = "Pillar 3a interest",
            StartDate = Start,
            Tiers = [new AccountInterestTier { FromAmount = 0m, AnnualRatePercent = 1m }]
        });

        var contract = CreateContract(payment.Id, pillar3aAccount.Id, amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 30),
            CloseContract = true,
            TargetAccountId = payment.Id
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        Assert.Contains(result.Warnings, x => x.Code == "PILLAR3A_CLOSE_IGNORES_GROWTH");
    }

    [Theory]
    // Money taken out of a tracked contract and spent outside the plan.
    [InlineData(true, false, TransactionKind.ExternalExpense)]
    // A contract the plan does not track paying into an account it does.
    [InlineData(false, true, TransactionKind.ExternalIncome)]
    // Both known: a plain transfer.
    [InlineData(true, true, TransactionKind.InternalTransfer)]
    public void Withdrawal_PostingShapeFollowsWhatIsKnown(
        bool linkContract,
        bool nameTarget,
        TransactionKind expectedKind)
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(openingBalance: 50_000m);

        var contract = CreateContract(
            payment.Id,
            accountId: linkContract ? pillar3aAccount.Id : null,
            amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 30),
            Amount = 10_000m,
            TargetAccountId = nameTarget ? payment.Id : null
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        var withdrawal = Assert.Single(
            result.Events,
            x => x.Category == "Pillar 3a Withdrawal");

        Assert.Equal(expectedKind, withdrawal.Kind);
    }

    [Fact]
    public void Withdrawal_WithNeitherAccountNorTarget_RaisesACritical()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);

        var contract = CreateContract(payment.Id, accountId: null, amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2026, 6, 30),
            Amount = 10_000m
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment],
            Start,
            End);

        var critical = Assert.Single(
            result.Warnings,
            x => x.Code == "PILLAR3A_WITHDRAWAL_NOT_POSTED");

        Assert.Equal(WarningSeverity.Critical, critical.Severity);
        Assert.DoesNotContain(result.Events, x => x.Category == "Pillar 3a Withdrawal");
    }

    [Fact]
    public void Withdrawal_OutsideTheSimulatedRange_IsNotGenerated()
    {
        var payment = TestPlanBuilder.CreateBankAccount(openingBalance: 20_000m);
        var pillar3aAccount = TestPlanBuilder.CreatePillar3aAccount(openingBalance: 50_000m);

        var contract = CreateContract(payment.Id, pillar3aAccount.Id, amount: 600m);

        contract.Withdrawals.Add(new Pillar3aWithdrawalEvent
        {
            Id = Guid.NewGuid(),
            Date = new DateOnly(2040, 6, 30),
            CloseContract = true,
            TargetAccountId = payment.Id
        });

        var result = new Pillar3aEventGenerator().Generate(
            [contract],
            [payment, pillar3aAccount],
            Start,
            End);

        Assert.DoesNotContain(result.Events, x => x.Category == "Pillar 3a Withdrawal");
        Assert.Equal(12, result.Events.Count);
    }

    private static Pillar3aContract CreateContract(
        Guid paymentAccountId,
        Guid? accountId,
        decimal amount,
        Guid? ownerPersonId = null)
    {
        return new Pillar3aContract
        {
            Id = Guid.NewGuid(),
            Name = "VIAC",
            OwnerPersonId = ownerPersonId ?? Guid.NewGuid(),
            AccountId = accountId,
            Type = Pillar3aContractType.Investment,
            OpeningValue = 0m,
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
                    Amount = amount,
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
