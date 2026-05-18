using CashFlowPlanner.BlazorWasm.Models;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class DashboardSummaryService
{
    public DashboardSummary CreateSummary(
        CashFlowPlan plan,
        SimulationResult result)
    {
        var endDate = plan.SimulationSettings.EndDate;

        var accountBalancesAtEnd = plan.Accounts
            .Where(account => account.Type != AccountType.Mortgage)
            .ToDictionary(
                account => account.Id,
                account => result.GetBalance(account.Id, endDate));

        var liquidAccountIds = plan.Accounts
            .Where(IsLiquidAccount)
            .Select(x => x.Id)
            .ToHashSet();

        var liquidAssets = accountBalancesAtEnd
            .Where(x => liquidAccountIds.Contains(x.Key))
            .Sum(x => x.Value);

        var accountLiabilities = accountBalancesAtEnd
            .Where(x => x.Value < 0)
            .Sum(x => Math.Abs(x.Value));

        var mortgageLiabilities = plan.Mortgages
            .Where(x => x.IsActive)
            .Sum(mortgage => GetProjectedMortgagePrincipal(
                mortgage,
                result,
                endDate));

        var totalLiabilities = accountLiabilities + mortgageLiabilities;

        var accountNetWorth = accountBalancesAtEnd.Values.Sum();

        var netWorth = accountNetWorth - mortgageLiabilities;

        var liquidBalanceByDate = result.BalancePoints
            .Where(point => liquidAccountIds.Contains(point.AccountId))
            .GroupBy(point => point.Date)
            .Select(group => new
            {
                Date = group.Key,
                Balance = group.Sum(x => x.Balance)
            })
            .OrderBy(x => x.Balance)
            .FirstOrDefault();

        return new DashboardSummary
        {
            LiquidAssets = liquidAssets,
            AccountLiabilities = accountLiabilities,
            MortgageLiabilities = mortgageLiabilities,
            TotalLiabilities = totalLiabilities,
            NetWorth = netWorth,
            LowestLiquidBalance = liquidBalanceByDate?.Balance ?? 0m,
            LowestLiquidBalanceDate = liquidBalanceByDate?.Date,
            WarningCount = result.Warnings.Count,
            CriticalWarningCount = result.Warnings.Count(x => x.Severity == WarningSeverity.Critical),
            Currency = plan.BaseCurrency
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