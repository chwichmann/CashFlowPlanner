using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

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
}
