namespace CashFlowPlanner.Core.Accounts;

public static class AccountStatementBuilder
{
    public static IReadOnlyList<AccountStatementRow> Build(
        Account account,
        IReadOnlyCollection<CashFlowEvent> events,
        DateOnly startDate,
        DateOnly endDate)
    {
        var relevantEvents = events
            .Where(x =>
                x.Date >= startDate &&
                x.Date <= endDate &&
                (x.FromAccountId == account.Id || x.ToAccountId == account.Id))
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();

        var balance = account.OpeningDate <= startDate
            ? account.OpeningBalance
            : 0m;

        var openingBalanceApplied = account.OpeningDate <= startDate;

        foreach (var cashFlowEvent in events
                     .Where(x =>
                         x.Date < startDate &&
                         (x.FromAccountId == account.Id || x.ToAccountId == account.Id))
                     .OrderBy(x => x.Date)
                     .ThenBy(x => x.Priority)
                     .ThenBy(x => x.Name))
        {
            ApplyOpeningBalanceIfNeeded(
                account,
                cashFlowEvent.Date,
                ref balance,
                ref openingBalanceApplied);

            ApplyEventToBalance(account.Id, cashFlowEvent, ref balance);
        }

        var rows = new List<AccountStatementRow>();

        if (!openingBalanceApplied &&
            account.OpeningDate >= startDate &&
            account.OpeningDate <= endDate)
        {
            balance += account.OpeningBalance;
            openingBalanceApplied = true;

            rows.Add(new AccountStatementRow
            {
                ValutaDate = account.OpeningDate,
                Title = "Opening balance",
                Incoming = null,
                Outgoing = null,
                Balance = balance,
                Currency = account.Currency,
                Category = "Opening Balance",
                Counterparty = null,
                Notes = null,
                SourceEventId = account.Id
            });
        }

        foreach (var cashFlowEvent in relevantEvents)
        {
            ApplyOpeningBalanceIfNeeded(
                account,
                cashFlowEvent.Date,
                ref balance,
                ref openingBalanceApplied);

            var incoming = cashFlowEvent.ToAccountId == account.Id
                ? cashFlowEvent.Amount
                : (decimal?)null;

            var outgoing = cashFlowEvent.FromAccountId == account.Id
                ? cashFlowEvent.Amount
                : (decimal?)null;

            ApplyEventToBalance(account.Id, cashFlowEvent, ref balance);

            rows.Add(new AccountStatementRow
            {
                ValutaDate = cashFlowEvent.Date,
                Title = cashFlowEvent.Name,
                Incoming = incoming,
                Outgoing = outgoing,
                Balance = balance,
                Currency = cashFlowEvent.Currency,
                Category = cashFlowEvent.Category,
                Counterparty = cashFlowEvent.Counterparty,
                Notes = cashFlowEvent.Notes,
                SourceEventId = cashFlowEvent.Id
            });
        }

        return rows
            .OrderBy(x => x.ValutaDate)
            .ThenBy(x => x.Title)
            .ToList();
    }

    private static void ApplyOpeningBalanceIfNeeded(
        Account account,
        DateOnly currentDate,
        ref decimal balance,
        ref bool openingBalanceApplied)
    {
        if (openingBalanceApplied)
        {
            return;
        }

        if (account.OpeningDate <= currentDate)
        {
            balance += account.OpeningBalance;
            openingBalanceApplied = true;
        }
    }

    private static void ApplyEventToBalance(
        Guid accountId,
        CashFlowEvent cashFlowEvent,
        ref decimal balance)
    {
        if (cashFlowEvent.ToAccountId == accountId)
        {
            balance += cashFlowEvent.Amount;
        }

        if (cashFlowEvent.FromAccountId == accountId)
        {
            balance -= cashFlowEvent.Amount;
        }
    }
}