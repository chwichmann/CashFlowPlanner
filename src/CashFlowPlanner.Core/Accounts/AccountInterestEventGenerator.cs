namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountInterestEventGenerator
{
    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<CashFlowEvent> existingEvents,
        DateOnly simulationStartDate,
        DateOnly simulationEndDate)
    {
        var generatedEvents = new List<CashFlowEvent>();

        foreach (var account in accounts)
        {
            foreach (var contract in account.InterestContracts.Where(x => x.IsActive))
            {
                contract.Validate();

                var periodStart = contract.StartDate > simulationStartDate
                    ? contract.StartDate
                    : simulationStartDate;

                var contractEnd = contract.EndDate is not null && contract.EndDate.Value < simulationEndDate
                    ? contract.EndDate.Value
                    : simulationEndDate;

                if (contractEnd < periodStart)
                {
                    continue;
                }

                var postingPeriods = GeneratePostingPeriods(
                    periodStart,
                    contractEnd,
                    contract.PostingFrequency);

                foreach (var postingPeriod in postingPeriods)
                {
                    var eventsForBalance = existingEvents
                        .Concat(generatedEvents)
                        .ToList();

                    var interest = CalculateInterestForPostingPeriod(
                        account,
                        contract,
                        eventsForBalance,
                        postingPeriod.StartDateInclusive,
                        postingPeriod.EndDateInclusive);

                    interest = Math.Round(interest, 2, MidpointRounding.AwayFromZero);

                    if (interest <= 0m)
                    {
                        continue;
                    }

                    generatedEvents.Add(new CashFlowEvent
                    {
                        Id = CreateDeterministicEventId(
                            account.Id,
                            contract.Id,
                            postingPeriod.EndDateInclusive),

                        SourceTransactionId = contract.Id,
                        Name = $"Interest: {account.Name}",
                        Date = postingPeriod.EndDateInclusive,
                        Kind = TransactionKind.ExternalIncome,
                        FromAccountId = null,
                        ToAccountId = account.Id,
                        Amount = interest,
                        Currency = account.Currency,
                        Priority = 900,
                        Category = "Interest",
                        Counterparty = "Bank",
                        PaymentMethod = PaymentMethod.BankTransfer,
                        Notes = $"Generated account interest from contract '{contract.Name}'."
                    });
                }
            }
        }

        return generatedEvents
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static decimal CalculateInterestForPostingPeriod(
        Account account,
        AccountInterestContract contract,
        IReadOnlyCollection<CashFlowEvent> eventsForBalance,
        DateOnly periodStartInclusive,
        DateOnly periodEndInclusive)
    {
        var interest = 0m;
        var currentDate = periodStartInclusive;

        while (currentDate <= periodEndInclusive)
        {
            var balance = GetEndOfPreviousDayBalance(
                account,
                eventsForBalance,
                currentDate);

            var nextDate = currentDate.AddDays(1);

            interest += AccountInterestCalculator.CalculateInterestForPeriod(
                balance,
                contract.Tiers,
                contract.CalculationMethod,
                contract.DayCountConvention,
                currentDate,
                nextDate);

            currentDate = nextDate;
        }

        return interest;
    }

    private static decimal GetEndOfPreviousDayBalance(
        Account account,
        IReadOnlyCollection<CashFlowEvent> eventsForBalance,
        DateOnly date)
    {
        // Same rule as SimulationEngine and AccountStatementBuilder: the opening
        // balance is only known from the account's opening date onwards. Without
        // this guard an account opened on 01.12 earned a full year of interest.
        var balance = account.OpeningDate <= date
            ? account.OpeningBalance
            : 0m;

        foreach (var cashFlowEvent in eventsForBalance
                     .Where(x => x.Date < date)
                     .OrderBy(x => x.Date)
                     .ThenBy(x => x.Priority)
                     .ThenBy(x => x.Name))
        {
            balance += AccountPosting.GetSignedAmount(account.Id, cashFlowEvent);
        }

        return balance;
    }

    private static IReadOnlyList<PostingPeriod> GeneratePostingPeriods(
        DateOnly startDate,
        DateOnly endDate,
        InterestPostingFrequency postingFrequency)
    {
        var result = new List<PostingPeriod>();
        var currentStart = startDate;

        while (currentStart <= endDate)
        {
            var naturalPeriodEnd = GetNaturalPostingPeriodEnd(
                currentStart,
                postingFrequency);

            var currentEnd = naturalPeriodEnd < endDate
                ? naturalPeriodEnd
                : endDate;

            result.Add(new PostingPeriod(
                currentStart,
                currentEnd));

            currentStart = currentEnd.AddDays(1);
        }

        return result;
    }

    private static DateOnly GetNaturalPostingPeriodEnd(
        DateOnly date,
        InterestPostingFrequency postingFrequency)
    {
        return postingFrequency switch
        {
            InterestPostingFrequency.Monthly =>
                new DateOnly(
                    date.Year,
                    date.Month,
                    DateTime.DaysInMonth(date.Year, date.Month)),

            InterestPostingFrequency.Quarterly =>
                GetQuarterEnd(date),

            InterestPostingFrequency.Yearly =>
                new DateOnly(date.Year, 12, 31),

            _ =>
                new DateOnly(date.Year, 12, 31)
        };
    }

    private static DateOnly GetQuarterEnd(DateOnly date)
    {
        var quarterEndMonth = date.Month switch
        {
            <= 3 => 3,
            <= 6 => 6,
            <= 9 => 9,
            _ => 12
        };

        return new DateOnly(
            date.Year,
            quarterEndMonth,
            DateTime.DaysInMonth(date.Year, quarterEndMonth));
    }

    private static Guid CreateDeterministicEventId(
        Guid accountId,
        Guid contractId,
        DateOnly postingDate)
    {
        var bytes = contractId.ToByteArray();

        var accountBytes = accountId.ToByteArray();
        var dayNumberBytes = BitConverter.GetBytes(postingDate.DayNumber);

        for (var i = 0; i < bytes.Length; i++)
        {
            bytes[i] ^= accountBytes[i];
        }

        for (var i = 0; i < dayNumberBytes.Length; i++)
        {
            bytes[i] ^= dayNumberBytes[i];
        }

        return new Guid(bytes);
    }

    private readonly record struct PostingPeriod(
        DateOnly StartDateInclusive,
        DateOnly EndDateInclusive);
}