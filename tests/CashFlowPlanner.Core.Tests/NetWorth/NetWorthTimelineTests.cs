using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.Tests.NetWorth;

/// <summary>
/// What the balance sheet reads on a day nothing happens, and on a day before a debt exists.
/// Both are answered by carrying a value forward, and carrying forward is exactly where the
/// direction can be got wrong.
/// </summary>
public sealed class NetWorthTimelineTests
{
    [Fact]
    public void NetWorth_DoesNotCollapse_OnDaysWithNoActivity()
    {
        var account = TestPlanBuilder.CreateBankAccount(
            openingBalance: 50_000m,
            openingDate: new DateOnly(2026, 1, 1));

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [account],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 1, 31));

        var result = new SimulationEngine().Simulate(plan);

        var quiet = result.NetWorthPoints.Where(p => p.Date >= new DateOnly(2026, 1, 10)).ToList();

        Assert.NotEmpty(quiet);
        Assert.All(quiet, p => Assert.Equal(50_000m, p.LiquidAssets));
    }

    /// <summary>
    /// The house-purchase case. The principal series has no point before completion, and the
    /// tracker answers a date before the first point with that first point - correct for a
    /// mortgage that already exists, and off by the entire loan for one that does not yet.
    /// </summary>
    [Fact]
    public void AMortgageThatStartsMidSimulation_ShowsNoDebt_BeforeItStarts()
    {
        var savings = TestPlanBuilder.CreateBankAccount(
            openingBalance: 100_000m,
            openingDate: new DateOnly(2026, 1, 1),
            name: "Savings");

        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "Future Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = savings.Id,
            InitialPrincipal = 600_000m,
            InitialDate = new DateOnly(2026, 7, 1),
            CalculationPrincipal = 600_000m,
            CalculationPrincipalDate = new DateOnly(2026, 7, 1),
            FixedInterestPercent = 1.5m,
            AmortisationMode = AmortisationMode.None,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [savings],
            mortgages: [mortgage],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        var beforeStart = result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 3, 1));
        var afterStart = result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 8, 1));

        Assert.Equal(0m, beforeStart.MortgagePrincipal);
        Assert.Equal(600_000m, afterStart.MortgagePrincipal);
    }

    /// <summary>
    /// The other half of the purchase. Fixing only the mortgage side would have made this
    /// worse than before: the debt would correctly wait for July while the house it paid for
    /// sat on the balance sheet from January, overstating net worth by the entire property
    /// rather than by the deposit.
    /// </summary>
    [Fact]
    public void APropertyAcquiredMidSimulation_IsNotAnAsset_BeforeItIsBought()
    {
        var savings = TestPlanBuilder.CreateBankAccount(
            openingBalance: 300_000m,
            openingDate: new DateOnly(2026, 1, 1),
            name: "Savings");

        var house = new RealEstateAsset
        {
            Name = "New House",
            Type = RealEstateType.House,
            CurrentEstimatedValue = 1_150_000m,
            AcquisitionDate = new DateOnly(2026, 7, 1)
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [savings],
            realEstateAssets: [house],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(
            0m,
            result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 6, 30)).RealEstateValue);

        Assert.Equal(
            1_150_000m,
            result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 7, 1)).RealEstateValue);
    }

    [Fact]
    public void APropertySold_StopsCounting_OnTheDisposalDate()
    {
        var savings = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1),
            name: "Savings");

        var house = new RealEstateAsset
        {
            Name = "Old Flat",
            Type = RealEstateType.Flat,
            CurrentEstimatedValue = 800_000m,
            DisposalDate = new DateOnly(2026, 9, 1)
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [savings],
            realEstateAssets: [house],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(
            800_000m,
            result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 8, 31)).RealEstateValue);

        Assert.Equal(
            0m,
            result.NetWorthPoints.Single(p => p.Date == new DateOnly(2026, 9, 1)).RealEstateValue);
    }

    [Fact]
    public void AProperty_WithNoOwnershipDates_CountsForTheWholeHorizon()
    {
        // The default, and the case for a household that already lives in the house.
        var savings = TestPlanBuilder.CreateBankAccount(
            openingBalance: 10_000m,
            openingDate: new DateOnly(2026, 1, 1),
            name: "Savings");

        var house = new RealEstateAsset
        {
            Name = "Family Home",
            Type = RealEstateType.House,
            CurrentEstimatedValue = 950_000m
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [savings],
            realEstateAssets: [house],
            startDate: new DateOnly(2026, 1, 1),
            endDate: new DateOnly(2026, 12, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.All(result.NetWorthPoints, p => Assert.Equal(950_000m, p.RealEstateValue));
    }

    [Fact]
    public void AProperty_DisposedOfBeforeItIsAcquired_IsRejected()
    {
        var house = new RealEstateAsset
        {
            Name = "Impossible",
            Type = RealEstateType.House,
            CurrentEstimatedValue = 100_000m,
            AcquisitionDate = new DateOnly(2026, 7, 1),
            DisposalDate = new DateOnly(2026, 3, 1)
        };

        var plan = TestPlanBuilder.CreatePlan(realEstateAssets: [house]);

        Assert.Throws<InvalidOperationException>(plan.Validate);
    }
}
