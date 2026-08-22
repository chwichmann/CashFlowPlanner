using System.Diagnostics;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// A guard against the shape of finding H6, which was a quadratic interest loop that took
/// 22.6 seconds natively on a ten-year plan. The browser runs this single-threaded in
/// WebAssembly, several times slower again, so a regression here does not read as "slow" to
/// the user - it reads as a frozen tab.
/// <para>
/// The budget is deliberately loose. This is not a benchmark and it must not fail because a
/// CI runner was busy; it exists to catch an algorithm that went from linear to quadratic,
/// which costs orders of magnitude rather than percentages.
/// </para>
/// </summary>
public sealed class SimulationPerformanceTests
{
    [Fact]
    public void AThirtyYearPlan_Simulates_InLinearTime()
    {
        var plan = CreateThirtyYearPlan();

        var engine = new SimulationEngine();

        // Warm up the JIT so the measurement is of the algorithm, not of first-call overhead.
        engine.Simulate(plan);

        var stopwatch = Stopwatch.StartNew();
        var result = engine.Simulate(plan);
        stopwatch.Stop();

        Assert.NotEmpty(result.NetWorthPoints);

        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(10),
            $"a 30-year plan took {stopwatch.Elapsed.TotalSeconds:N1}s natively. "
            + "In WebAssembly that is a frozen tab - look for a loop that became quadratic.");
    }

    private static CashFlowPlan CreateThirtyYearPlan()
    {
        var start = new DateOnly(2026, 1, 1);
        var end = new DateOnly(2056, 12, 31);

        var accounts = new List<Account>();
        var transactions = new List<TransactionDefinition>();

        for (var i = 0; i < 8; i++)
        {
            var account = TestPlanBuilder.CreateBankAccount(
                openingBalance: 20_000m,
                openingDate: start,
                name: $"Account {i}");

            // Interest is the loop H6 was about: accrual against a balance series that itself
            // grows with every event generated. Without a contract on these accounts the
            // guard would be watching the wrong code.
            account.InterestContracts.Add(new AccountInterestContract
            {
                Name = $"Interest {i}",
                CalculationMethod = AccountInterestCalculationMethod.FlatBalance,
                PostingFrequency = InterestPostingFrequency.Yearly,
                DayCountConvention = InterestDayCountConvention.Actual360,
                StartDate = start,
                Tiers =
                [
                    new AccountInterestTier
                    {
                        FromAmount = 0m,
                        ToAmount = null,
                        AnnualRatePercent = 0.75m
                    }
                ]
            });

            accounts.Add(account);

            for (var t = 0; t < 5; t++)
            {
                transactions.Add(new TransactionDefinition
                {
                    Id = Guid.NewGuid(),
                    Name = $"Charge {i}-{t}",
                    Kind = TransactionKind.ExternalExpense,
                    FromAccountId = account.Id,
                    Amount = 250m,
                    Currency = "CHF",
                    IndexationMode = IndexationMode.PlanDefault,
                    Schedule = new Schedule
                    {
                        Frequency = ScheduleFrequency.Monthly,
                        StartDate = start,
                        DayOfMonth = 1 + t,
                        BusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay
                    }
                });
            }
        }

        return TestPlanBuilder.CreatePlan(
            accounts: accounts,
            transactions: transactions,
            realEstateAssets:
            [
                new RealEstateAsset
                {
                    Name = "House",
                    Type = RealEstateType.House,
                    CurrentEstimatedValue = 1_100_000m,
                    ValuationDate = start,
                    AnnualValueGrowthPercent = 1.2m
                }
            ],
            startDate: start,
            endDate: end,
            inflation: new InflationAssumption
            {
                AnnualRatePercent = 1.4m,
                BaseDate = start
            });
    }
}
