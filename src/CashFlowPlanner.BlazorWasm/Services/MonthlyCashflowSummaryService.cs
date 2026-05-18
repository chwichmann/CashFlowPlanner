using CashFlowPlanner.BlazorWasm.Models;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class MonthlyCashflowSummaryService
{
    public IReadOnlyList<MonthlyCashflowSummary> CreateMonthlySummary(
        CashFlowPlan plan,
        SimulationResult result)
    {
        var liquidAccountIds = plan.Accounts
            .Where(IsLiquidAccount)
            .Select(x => x.Id)
            .ToHashSet();

        var mortgageIds = plan.Mortgages
            .Select(x => x.Id)
            .ToHashSet();

        var months = EnumerateMonths(
                plan.SimulationSettings.StartDate,
                plan.SimulationSettings.EndDate)
            .ToList();

        var summaries = new List<MonthlyCashflowSummary>();

        foreach (var monthStart in months)
        {
            var monthEnd = GetMonthEnd(
                monthStart,
                plan.SimulationSettings.EndDate);

            var monthEvents = result.Events
                .Where(x => x.Date >= monthStart && x.Date <= monthEnd)
                .ToList();

            var income = monthEvents
                .Where(x => x.Kind == TransactionKind.ExternalIncome)
                .Sum(x => x.Amount);

            var expenses = monthEvents
                .Where(x => x.Kind is TransactionKind.ExternalExpense or TransactionKind.DebtPayment)
                .Sum(x => x.Amount);

            var internalTransfersOut = monthEvents
                .Where(x => x.Kind == TransactionKind.InternalTransfer)
                .Where(x => x.FromAccountId is not null && liquidAccountIds.Contains(x.FromAccountId.Value))
                .Sum(x => x.Amount);

            var internalTransfersIn = monthEvents
                .Where(x => x.Kind == TransactionKind.InternalTransfer)
                .Where(x => x.ToAccountId is not null && liquidAccountIds.Contains(x.ToAccountId.Value))
                .Sum(x => x.Amount);

            var mortgagePayments = monthEvents
                .Where(x => mortgageIds.Contains(x.SourceTransactionId))
                .Sum(x => x.Amount);

            var netCashflow = income
                - expenses
                - internalTransfersOut
                + internalTransfersIn;

            var endLiquidBalance = result.BalancePoints
                .Where(x => x.Date == monthEnd)
                .Where(x => liquidAccountIds.Contains(x.AccountId))
                .Sum(x => x.Balance);

            var warningCount = result.Warnings
                .Count(x => x.Date >= monthStart && x.Date <= monthEnd);

            summaries.Add(new MonthlyCashflowSummary
            {
                Year = monthStart.Year,
                Month = monthStart.Month,
                MonthStart = monthStart,
                MonthEnd = monthEnd,
                Income = income,
                Expenses = expenses,
                InternalTransfersOut = internalTransfersOut,
                InternalTransfersIn = internalTransfersIn,
                MortgagePayments = mortgagePayments,
                NetCashflow = netCashflow,
                EndLiquidBalance = endLiquidBalance,
                WarningCount = warningCount,
                Currency = plan.BaseCurrency
            });
        }

        return summaries;
    }

    private static IEnumerable<DateOnly> EnumerateMonths(
        DateOnly startDate,
        DateOnly endDate)
    {
        var current = new DateOnly(startDate.Year, startDate.Month, 1);
        var end = new DateOnly(endDate.Year, endDate.Month, 1);

        while (current <= end)
        {
            yield return current;
            current = current.AddMonths(1);
        }
    }

    private static DateOnly GetMonthEnd(
        DateOnly monthStart,
        DateOnly simulationEnd)
    {
        var naturalMonthEnd = monthStart.AddMonths(1).AddDays(-1);

        return naturalMonthEnd <= simulationEnd
            ? naturalMonthEnd
            : simulationEnd;
    }

    private static bool IsLiquidAccount(Account account)
    {
        return account.Type is AccountType.BankAccount
            or AccountType.SavingsAccount
            or AccountType.Cash;
    }
}