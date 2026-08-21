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

            balance += AccountPosting.GetSignedAmount(account.Id, cashFlowEvent);
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

            var signedAmount = AccountPosting.GetSignedAmount(account.Id, cashFlowEvent);

            var incoming = signedAmount > 0m
                ? signedAmount
                : (decimal?)null;

            var outgoing = signedAmount < 0m
                ? -signedAmount
                : (decimal?)null;

            balance += signedAmount;

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

        // No re-sort here. The running balance was accumulated in
        // (Date, Priority, Name) order -- the same order the simulation engine
        // applies events in -- so re-sorting by (ValutaDate, Title) afterwards left
        // the balance column not reconciling with the rows it sits next to.
        return rows;
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
}