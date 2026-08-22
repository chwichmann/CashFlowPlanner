using CashFlowPlanner.BlazorWasm.Models;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.NetWorth;

namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>
/// The dashboard's headline figures.
///
/// The balance sheet comes from <see cref="SimulationResult.NetWorthPoints"/> whenever the
/// simulation produced one. This service used to compute its own - accounts, minus mortgage
/// principal - which was a second implementation of net worth that could disagree with the chart
/// beside it, and which knew nothing about the household's property. A wrong total is now
/// traceable to one component rather than to two different sums.
/// </summary>
public sealed class DashboardSummaryService
{
    public DashboardSummary CreateSummary(
        CashFlowPlan plan,
        SimulationResult result,
        AmountBasis basis = AmountBasis.Nominal)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(result);

        var endDate = plan.SimulationSettings.EndDate;

        var liquidAccountIds = plan.Accounts
            .Where(IsLiquidAccount)
            .Select(x => x.Id)
            .ToHashSet();

        var lowestLiquid = result.BalancePoints
            .Where(point => liquidAccountIds.Contains(point.AccountId))
            .GroupBy(point => point.Date)
            .Select(group => new
            {
                Date = group.Key,
                Balance = group.Sum(x => x.Balance)
            })
            .OrderBy(x => x.Balance)
            .FirstOrDefault();

        var warningCount = result.Warnings.Count;
        var criticalWarningCount = result.Warnings.Count(x => x.Severity == WarningSeverity.Critical);

        var point = FindPoint(result, endDate, basis);

        var lowestLiquidBalance = lowestLiquid is null
            ? 0m
            : result.ToBasis(lowestLiquid.Balance, lowestLiquid.Date, basis);

        if (point is null)
        {
            return CreateLegacySummary(
                plan,
                result,
                endDate,
                liquidAccountIds,
                lowestLiquidBalance,
                lowestLiquid?.Date,
                warningCount,
                criticalWarningCount,
                basis);
        }

        return new DashboardSummary
        {
            LiquidAssets = point.LiquidAssets,
            InvestmentAssets = point.InvestmentAssets,
            Pillar3aAssets = point.Pillar3aAssets,
            RealEstateValue = point.RealEstateValue,
            TotalAssets = point.TotalAssets,

            AccountLiabilities = point.OtherLiabilities,
            MortgageLiabilities = point.MortgagePrincipal,
            TotalLiabilities = point.TotalLiabilities,

            NetWorth = point.NetWorth,

            LowestLiquidBalance = lowestLiquidBalance,
            LowestLiquidBalanceDate = lowestLiquid?.Date,

            WarningCount = warningCount,
            CriticalWarningCount = criticalWarningCount,

            Currency = point.Currency,
            Basis = basis,
            AsOf = point.Date,
            HasNetWorthSeries = true
        };
    }

    /// <summary>
    /// The balance sheet on <paramref name="date"/> in the requested basis, or <c>null</c> when
    /// the result carries no series at all.
    /// </summary>
    private static NetWorthPoint? FindPoint(
        SimulationResult result,
        DateOnly date,
        AmountBasis basis)
    {
        var points = result.GetNetWorthPoints(basis);

        if (points.Count == 0)
        {
            return null;
        }

        NetWorthPoint? atOrBefore = null;

        foreach (var candidate in points)
        {
            if (candidate.Date <= date &&
                (atOrBefore is null || candidate.Date > atOrBefore.Date))
            {
                atOrBefore = candidate;
            }
        }

        // A horizon that ends before the first point is not a case the engine produces, but a
        // hand-built result can; the earliest figure beats no figure.
        return atOrBefore ?? points[0];
    }

    /// <summary>
    /// What this service computed before the net-worth series existed: accounts, minus mortgage
    /// principal. Kept for a result that carries no series - a fixture, or one deserialized from
    /// an older session - and deliberately not extended, because it cannot see the property.
    /// </summary>
    private static DashboardSummary CreateLegacySummary(
        CashFlowPlan plan,
        SimulationResult result,
        DateOnly endDate,
        HashSet<Guid> liquidAccountIds,
        decimal lowestLiquidBalance,
        DateOnly? lowestLiquidBalanceDate,
        int warningCount,
        int criticalWarningCount,
        AmountBasis basis)
    {
        var accountBalancesAtEnd = plan.Accounts
            .Where(account => account.Type != AccountType.Mortgage)
            .ToDictionary(
                account => account.Id,
                account => result.ToBasis(result.GetBalance(account.Id, endDate), endDate, basis));

        var liquidAssets = accountBalancesAtEnd
            .Where(x => liquidAccountIds.Contains(x.Key))
            .Sum(x => x.Value);

        var accountLiabilities = accountBalancesAtEnd
            .Where(x => x.Value < 0)
            .Sum(x => Math.Abs(x.Value));

        var mortgageLiabilities = plan.Mortgages
            .Where(x => x.IsActive)
            .Sum(mortgage => result.ToBasis(
                GetProjectedMortgagePrincipal(mortgage, result, endDate),
                endDate,
                basis));

        var accountNetWorth = accountBalancesAtEnd.Values.Sum();

        return new DashboardSummary
        {
            LiquidAssets = liquidAssets,
            TotalAssets = accountBalancesAtEnd.Values.Where(x => x > 0).Sum(),
            AccountLiabilities = accountLiabilities,
            MortgageLiabilities = mortgageLiabilities,
            TotalLiabilities = accountLiabilities + mortgageLiabilities,
            NetWorth = accountNetWorth - mortgageLiabilities,
            LowestLiquidBalance = lowestLiquidBalance,
            LowestLiquidBalanceDate = lowestLiquidBalanceDate,
            WarningCount = warningCount,
            CriticalWarningCount = criticalWarningCount,
            Currency = plan.BaseCurrency,
            Basis = basis,
            AsOf = endDate,
            HasNetWorthSeries = false
        };
    }

    private static decimal GetProjectedMortgagePrincipal(
        MortgageContract mortgage,
        SimulationResult result,
        DateOnly date)
    {
        var projectedPrincipal = result.GetMortgagePrincipal(
            mortgage.Id,
            date);

        if (projectedPrincipal > 0)
        {
            return projectedPrincipal;
        }

        return mortgage.CalculationPrincipal ?? mortgage.InitialPrincipal;
    }

    private static bool IsLiquidAccount(Account account)
    {
        return account.Type is AccountType.BankAccount
            or AccountType.SavingsAccount
            or AccountType.Cash;
    }
}
