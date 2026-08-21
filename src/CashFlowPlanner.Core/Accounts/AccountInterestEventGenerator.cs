namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountInterestEventGenerator
{
    /// <summary>
    /// Accrues interest by walking the plan's events forward ONCE per account
    /// behind a balance cursor.
    ///
    /// This used to snapshot <c>existingEvents.Concat(generatedEvents)</c> for
    /// every posting period and then re-filter and re-sort that entire list for
    /// every single day of the period -- O(D * N log N). On a 10-year plan with
    /// ~17'000 events that was ~9.5 s of a ~9.7 s simulation; the same plan
    /// without interest contracts ran in ~0.19 s. It is now O(N + D).
    ///
    /// The compounding rules it has to reproduce exactly:
    /// <list type="bullet">
    /// <item>a day accrues on the balance as of the END of the previous day, so
    /// only events dated strictly before that day count;</item>
    /// <item>a contract sees the interest it has itself already posted;</item>
    /// <item>a later contract on the same account additionally sees what the
    /// earlier contracts on that account posted.</item>
    /// </list>
    ///
    /// Ordering inside a day is irrelevant to the result: the balance is a plain
    /// sum of every posting before the day, and adding decimals is exact and
    /// order independent. That is what makes the cursor equivalent to the old
    /// per-day re-sort.
    /// </summary>
    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<CashFlowEvent> existingEvents,
        DateOnly simulationStartDate,
        DateOnly simulationEndDate)
    {
        var generatedEvents = new List<CashFlowEvent>();

        // Interest posted earlier in this run, per account. Tiny: at most one
        // entry per posting period per contract.
        var generatedPostingsByAccount = new Dictionary<Guid, List<DatedAmount>>();

        foreach (var account in accounts)
        {
            var activeContracts = account.InterestContracts
                .Where(x => x.IsActive)
                .ToList();

            if (activeContracts.Count == 0)
            {
                continue;
            }

            // The one and only pass over the plan's events for this account.
            var existingLedger = BalanceLedger.ForAccount(account.Id, existingEvents);

            foreach (var contract in activeContracts)
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

                // What the contracts processed before this one already posted on
                // this account, frozen here -- exactly like the old snapshot of
                // existingEvents.Concat(generatedEvents) was frozen per period.
                var priorLedger = generatedPostingsByAccount.TryGetValue(account.Id, out var priorPostings)
                    ? BalanceLedger.ForPostings(priorPostings)
                    : BalanceLedger.Empty;

                var existingCursor = 0;
                var priorCursor = 0;

                // Interest THIS contract has posted so far. Every posting is dated
                // on a period end and periods are contiguous, so by the time the
                // walk reaches the next period every own posting is strictly in
                // the past. No cursor needed.
                var ownPostedInterest = 0m;

                foreach (var postingPeriod in postingPeriods)
                {
                    var interest = 0m;

                    for (var currentDate = postingPeriod.StartDateInclusive;
                         currentDate <= postingPeriod.EndDateInclusive;
                         currentDate = currentDate.AddDays(1))
                    {
                        // Same rule as SimulationEngine and AccountStatementBuilder:
                        // the opening balance is only known from the account's
                        // opening date onwards. Without this guard an account
                        // opened on 01.12 earned a full year of interest.
                        var balance =
                            (account.OpeningDate <= currentDate ? account.OpeningBalance : 0m) +
                            existingLedger.SumBefore(currentDate, ref existingCursor) +
                            priorLedger.SumBefore(currentDate, ref priorCursor) +
                            ownPostedInterest;

                        interest += AccountInterestCalculator.CalculateInterestForPeriod(
                            balance,
                            contract.Tiers,
                            contract.CalculationMethod,
                            contract.DayCountConvention,
                            currentDate,
                            currentDate.AddDays(1));
                    }

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

                    // An interest event is ExternalIncome onto this very account,
                    // so it moves the balance by exactly +interest.
                    ownPostedInterest += interest;

                    if (!generatedPostingsByAccount.TryGetValue(account.Id, out var postings))
                    {
                        postings = [];
                        generatedPostingsByAccount[account.Id] = postings;
                    }

                    postings.Add(new DatedAmount(
                        postingPeriod.EndDateInclusive,
                        interest));
                }
            }
        }

        return generatedEvents
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
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

    private readonly record struct DatedAmount(
        DateOnly Date,
        decimal Amount);

    /// <summary>
    /// Every posting that hits one account, ordered by date, with prefix sums.
    /// Answers "what did this account move before day X" in one cursor advance
    /// and one array read, instead of a filter-and-sort over the whole plan.
    /// </summary>
    private sealed class BalanceLedger
    {
        public static readonly BalanceLedger Empty = new([], [0m]);

        private readonly DateOnly[] _dates;

        /// <summary>
        /// <c>_prefixSums[i]</c> is the sum of the first <c>i</c> postings, so it
        /// has one more entry than <see cref="_dates"/>.
        /// </summary>
        private readonly decimal[] _prefixSums;

        private BalanceLedger(DateOnly[] dates, decimal[] prefixSums)
        {
            _dates = dates;
            _prefixSums = prefixSums;
        }

        public static BalanceLedger ForAccount(
            Guid accountId,
            IReadOnlyCollection<CashFlowEvent> events)
        {
            var postings = new List<DatedAmount>();

            foreach (var cashFlowEvent in events)
            {
                var amount = AccountPosting.GetSignedAmount(accountId, cashFlowEvent);

                if (amount != 0m)
                {
                    postings.Add(new DatedAmount(cashFlowEvent.Date, amount));
                }
            }

            return ForPostings(postings);
        }

        public static BalanceLedger ForPostings(List<DatedAmount> postings)
        {
            if (postings.Count == 0)
            {
                return Empty;
            }

            // Copied on purpose: the caller keeps appending to its list while the
            // ledger is in use, and the ledger must stay the frozen snapshot.
            var ordered = postings.ToArray();

            // The caller usually hands these over already sorted, but the public
            // GenerateEvents overload does not promise it. An unstable sort is
            // fine because only the SUM per date boundary is read, and adding
            // decimals is exact and order independent.
            Array.Sort(ordered, static (left, right) => left.Date.CompareTo(right.Date));

            var dates = new DateOnly[ordered.Length];
            var prefixSums = new decimal[ordered.Length + 1];

            for (var i = 0; i < ordered.Length; i++)
            {
                dates[i] = ordered[i].Date;
                prefixSums[i + 1] = prefixSums[i] + ordered[i].Amount;
            }

            return new BalanceLedger(dates, prefixSums);
        }

        /// <summary>
        /// Sum of every posting dated strictly before <paramref name="date"/>.
        /// <paramref name="cursor"/> is the caller's position in this ledger and
        /// must only ever be called with non-decreasing dates -- that is what
        /// makes the whole walk linear.
        /// </summary>
        public decimal SumBefore(DateOnly date, ref int cursor)
        {
            while (cursor < _dates.Length && _dates[cursor] < date)
            {
                cursor++;
            }

            return _prefixSums[cursor];
        }
    }
}