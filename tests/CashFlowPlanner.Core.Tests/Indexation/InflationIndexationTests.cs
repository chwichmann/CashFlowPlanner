using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.Core.Tests.Indexation;

/// <summary>
/// Gap 2: there was no inflation anywhere in the domain, so a 30-year plan
/// understated every expense by the compounded rate.
/// </summary>
public sealed class InflationIndexationTests
{
    private static readonly DateOnly BaseDate = new(2026, 1, 1);

    [Fact]
    public void Simulate_NoInflationAssumption_LeavesEveryAmountUntouched()
    {
        var result = SimulateGroceries(
            inflation: new InflationAssumption(),
            end: new DateOnly(2030, 12, 31));

        Assert.All(result.Events, e =>
        {
            Assert.Equal(1_000m, e.Amount);
            Assert.Equal(1m, e.IndexationFactor);
        });
    }

    [Theory]
    // The base year and every month in it: no step yet.
    [InlineData(2026, 1, 1_000)]
    [InlineData(2026, 12, 1_000)]
    // One step per anniversary, compounding on the previous year's amount.
    [InlineData(2027, 1, 1_020)]
    [InlineData(2027, 12, 1_020)]
    [InlineData(2028, 6, 1_040.40)]
    [InlineData(2030, 6, 1_082.432160)]
    public void Simulate_IndexedExpense_StepsOnceAYearOnTheAnniversary(
        int year,
        int month,
        decimal expected)
    {
        var result = SimulateGroceries(
            inflation: new InflationAssumption
            {
                AnnualRatePercent = 2m,
                BaseDate = BaseDate
            },
            end: new DateOnly(2030, 12, 31));

        var occurrence = result.Events.Single(e =>
            e.Date.Year == year && e.Date.Month == month);

        Assert.Equal(expected, occurrence.Amount);
    }

    /// <summary>
    /// Compounding is annual, not per occurrence: 60 monthly charges over five
    /// years produce five distinct amounts, not sixty.
    /// </summary>
    [Fact]
    public void Simulate_IndexedMonthlyExpense_ProducesOneAmountPerYear()
    {
        var result = SimulateGroceries(
            inflation: new InflationAssumption
            {
                AnnualRatePercent = 2m,
                BaseDate = BaseDate
            },
            end: new DateOnly(2030, 12, 31));

        Assert.Equal(60, result.Events.Count);
        Assert.Equal(5, result.Events.Select(e => e.Amount).Distinct().Count());
    }

    /// <summary>
    /// The reason a plan-wide rate is not enough on its own: a fixed-rate
    /// mortgage instalment does not index, and rent does.
    /// </summary>
    [Theory]
    [InlineData(IndexationMode.PlanDefault, null, 1_020)]
    [InlineData(IndexationMode.None, null, 1_000)]
    [InlineData(IndexationMode.Custom, 5.0, 1_050)]
    // A negative custom rate models a cost that falls -- a shrinking loan
    // servicing charge, a tariff being phased down.
    [InlineData(IndexationMode.Custom, -3.0, 970)]
    public void Simulate_PerTransactionOverride_BeatsThePlanRate(
        IndexationMode mode,
        double? customRate,
        decimal expectedIn2027)
    {
        var account = TestPlanBuilder.CreateBankAccount(openingBalance: 500_000m);

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Expense",
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = account.Id,
            Amount = 1_000m,
            Currency = "CHF",
            IndexationMode = mode,
            AnnualIndexationRatePercent = customRate is null
                ? null
                : (decimal)customRate.Value,
            Schedule = TestPlanBuilder.Monthly(BaseDate, dayOfMonth: 15)
        };

        var plan = CreatePlan(
            account,
            transaction,
            new InflationAssumption { AnnualRatePercent = 2m, BaseDate = BaseDate },
            new DateOnly(2027, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        var occurrence = result.Events.Single(e => e.Date == new DateOnly(2027, 6, 15));

        Assert.Equal(expectedIn2027, occurrence.Amount);
    }

    /// <summary>
    /// Salary progression is the same mechanism from the income side: an income
    /// with its own rate rises against the plan's general inflation.
    /// </summary>
    [Fact]
    public void Simulate_IncomeWithItsOwnRate_ModelsSalaryProgression()
    {
        var account = TestPlanBuilder.CreateBankAccount();

        var salary = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Salary",
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = account.Id,
            Amount = 8_000m,
            Currency = "CHF",
            IndexationMode = IndexationMode.Custom,
            AnnualIndexationRatePercent = 2.5m,
            Schedule = TestPlanBuilder.Monthly(BaseDate, dayOfMonth: 25)
        };

        var plan = CreatePlan(
            account,
            salary,
            new InflationAssumption { AnnualRatePercent = 1m, BaseDate = BaseDate },
            new DateOnly(2028, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(8_000m, result.Events.Single(e => e.Date == new DateOnly(2026, 6, 25)).Amount);
        Assert.Equal(8_200m, result.Events.Single(e => e.Date == new DateOnly(2027, 6, 25)).Amount);
        Assert.Equal(8_405m, result.Events.Single(e => e.Date == new DateOnly(2028, 6, 25)).Amount);
    }

    /// <summary>
    /// An amount can be stated in the money of a date other than the plan's
    /// base date -- a salary last negotiated two years ago, say.
    /// </summary>
    [Fact]
    public void Simulate_TransactionWithItsOwnBaseDate_CompoundsFromThatDate()
    {
        var account = TestPlanBuilder.CreateBankAccount(openingBalance: 500_000m);

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Rent",
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = account.Id,
            Amount = 1_000m,
            Currency = "CHF",
            IndexationBaseDate = new DateOnly(2024, 1, 1),
            Schedule = TestPlanBuilder.Monthly(BaseDate, dayOfMonth: 1)
        };

        var plan = CreatePlan(
            account,
            transaction,
            new InflationAssumption { AnnualRatePercent = 2m, BaseDate = BaseDate },
            new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        // Two completed years since 2024, so the 2026 charge is already stepped
        // twice even though the plan's own base date is 2026.
        Assert.Equal(1_040.40m, result.Events.Single(e => e.Date == new DateOnly(2026, 6, 1)).Amount);
    }

    [Fact]
    public void Validate_InflationRateWithoutABaseDate_Throws()
    {
        var plan = TestPlanBuilder.CreatePlan();

        var withRate = new CashFlowPlan
        {
            Id = plan.Id,
            Name = plan.Name,
            SimulationSettings = plan.SimulationSettings,
            Inflation = new InflationAssumption { AnnualRatePercent = 2m }
        };

        var error = Assert.Throws<InvalidOperationException>(withRate.Validate);

        Assert.Contains("states no base date", error.Message);
    }

    [Fact]
    public void Validate_CustomIndexationWithoutARate_Throws()
    {
        var account = TestPlanBuilder.CreateBankAccount();

        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Expense",
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = account.Id,
            Amount = 100m,
            Currency = "CHF",
            IndexationMode = IndexationMode.Custom,
            Schedule = TestPlanBuilder.Once(BaseDate)
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            transactions: [transaction]);

        var error = Assert.Throws<InvalidOperationException>(plan.Validate);

        Assert.Contains("custom indexation rate but does not state one", error.Message);
    }

    /// <summary>
    /// Real vs nominal is a presentation choice on the result. The engine posts
    /// nominal amounts; asking for real terms deflates them back to the base
    /// date and must not change what the engine computed.
    /// </summary>
    [Fact]
    public void GetNetWorthPoints_RealBasis_DeflatesTheWholeBalanceSheet()
    {
        var account = TestPlanBuilder.CreateBankAccount(openingBalance: 100_000m);

        var plan = new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Plan",
            BaseCurrency = "CHF",
            Accounts = [account],
            Inflation = new InflationAssumption
            {
                AnnualRatePercent = 2m,
                BaseDate = BaseDate
            },
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = BaseDate,
                EndDate = new DateOnly(2036, 12, 31)
            }
        };

        var result = new SimulationEngine().Simulate(plan);

        var date = new DateOnly(2036, 6, 30);

        var nominal = result
            .GetNetWorthPoints(AmountBasis.Nominal)
            .Single(p => p.Date == date);

        var real = result
            .GetNetWorthPoints(AmountBasis.Real)
            .Single(p => p.Date == date);

        // Nothing happens to the account, so the nominal balance never moves.
        Assert.Equal(100_000m, nominal.NetWorth);
        Assert.Equal(100_000m, nominal.LiquidAssets);

        // Ten completed years at 2%: 1.02^10 = 1.2189944...
        Assert.Equal(82_034.83m, Math.Round(real.NetWorth, 2));
        Assert.Equal(real.LiquidAssets, real.NetWorth);

        // The presentation call did not mutate what the engine produced.
        Assert.Equal(100_000m, result.NetWorthPoints.Single(p => p.Date == date).NetWorth);
    }

    [Fact]
    public void ToBasis_WithNoInflationAssumption_ReturnsTheSameNumberForBothBases()
    {
        var result = SimulateGroceries(
            inflation: new InflationAssumption(),
            end: new DateOnly(2030, 12, 31));

        var date = new DateOnly(2030, 1, 1);

        Assert.Equal(1_000m, result.ToBasis(1_000m, date, AmountBasis.Nominal));
        Assert.Equal(1_000m, result.ToBasis(1_000m, date, AmountBasis.Real));
    }

    private static SimulationResult SimulateGroceries(
        InflationAssumption inflation,
        DateOnly end)
    {
        var account = TestPlanBuilder.CreateBankAccount(openingBalance: 500_000m);

        var groceries = TestPlanBuilder.ExternalExpense(
            account.Id,
            1_000m,
            TestPlanBuilder.Monthly(BaseDate, dayOfMonth: 15),
            name: "Groceries");

        return new SimulationEngine().Simulate(
            CreatePlan(account, groceries, inflation, end));
    }

    private static CashFlowPlan CreatePlan(
        Account account,
        TransactionDefinition transaction,
        InflationAssumption inflation,
        DateOnly end)
    {
        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Test plan",
            BaseCurrency = "CHF",
            Accounts = [account],
            Transactions = [transaction],
            Inflation = inflation,
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = BaseDate,
                EndDate = end,
                Granularity = SimulationGranularity.Daily,
                WarnOnNegativeBankBalance = false
            }
        };
    }
}
