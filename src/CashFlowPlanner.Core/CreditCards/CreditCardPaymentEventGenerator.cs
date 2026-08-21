using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.CreditCards;

public sealed class CreditCardPaymentEventGenerator
{
    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IEnumerable<CreditCardContract> creditCards,
        IReadOnlyList<Account> accounts,
        IReadOnlyList<CashFlowEvent> baseEvents,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var result = new List<CashFlowEvent>();

        var accountById = accounts.ToDictionary(x => x.Id);

        foreach (var creditCard in creditCards.Where(x => x.IsActive))
        {
            creditCard.Validate();

            if (!accountById.TryGetValue(creditCard.CreditCardAccountId, out var creditCardAccount))
            {
                throw new InvalidOperationException(
                    $"Credit card contract '{creditCard.Name}' references unknown credit card account.");
            }

            var events = GenerateEventsForCreditCard(
                creditCard,
                creditCardAccount,
                baseEvents,
                simulationStart,
                simulationEnd);

            result.AddRange(events);
        }

        return result
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }

    /// <summary>
    /// Same quadratic shape as the account interest generator had: this used to
    /// concatenate and re-sort every event in the plan, then re-filter it, once
    /// per closing date. Closing dates are enumerated in ascending order, so the
    /// card account's postings are now walked once behind a cursor.
    ///
    /// The generated payments are re-scanned in full on every closing date, on
    /// purpose: a payment can be dated AFTER the closing date it settles, so that
    /// sequence is not guaranteed to be monotonic. It holds at most one entry per
    /// month, so the cost is irrelevant.
    /// </summary>
    private static IReadOnlyList<CashFlowEvent> GenerateEventsForCreditCard(
        CreditCardContract creditCard,
        Account creditCardAccount,
        IReadOnlyList<CashFlowEvent> baseEvents,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var generatedPaymentEvents = new List<CashFlowEvent>();

        var ledger = CardAccountLedger.Build(
            creditCard.CreditCardAccountId,
            baseEvents);

        var ledgerCursor = 0;

        foreach (var closingDate in EnumerateClosingDates(
            creditCard,
            simulationStart,
            simulationEnd))
        {
            var paymentDate = CreatePaymentDate(creditCard, closingDate);

            paymentDate = ApplyBusinessDayAdjustment(
                paymentDate,
                creditCard.PaymentBusinessDayAdjustment);

            if (paymentDate < simulationStart || paymentDate > simulationEnd)
            {
                continue;
            }

            if (creditCard.EndDate is not null && paymentDate > creditCard.EndDate.Value)
            {
                continue;
            }

            var balanceAtClosing =
                (creditCardAccount.OpeningDate <= closingDate
                    ? creditCardAccount.OpeningBalance
                    : 0m) +
                ledger.SumUpToAndIncluding(closingDate, ref ledgerCursor) +
                SumGeneratedPayments(
                    creditCard.CreditCardAccountId,
                    generatedPaymentEvents,
                    closingDate);

            if (balanceAtClosing >= 0)
            {
                continue;
            }

            var paymentAmount = Math.Abs(balanceAtClosing);

            var paymentEvent = new CashFlowEvent
            {
                SourceTransactionId = creditCard.Id,
                Name = $"{creditCard.Name} payment",
                Date = paymentDate,
                Kind = TransactionKind.DebtPayment,
                FromAccountId = creditCard.PaymentAccountId,
                ToAccountId = creditCard.CreditCardAccountId,
                Amount = paymentAmount,
                Currency = creditCardAccount.Currency,
                Priority = 90,
                Category = "Credit Card Payment",
                Counterparty = creditCard.Name,
                PaymentMethod = MapPaymentMethod(creditCard.PaymentMethod),
                Notes = "Generated from credit card contract."
            };

            generatedPaymentEvents.Add(paymentEvent);
        }

        return generatedPaymentEvents;
    }

    private static decimal SumGeneratedPayments(
        Guid creditCardAccountId,
        List<CashFlowEvent> generatedPaymentEvents,
        DateOnly closingDate)
    {
        var sum = 0m;

        foreach (var paymentEvent in generatedPaymentEvents)
        {
            if (paymentEvent.Date <= closingDate)
            {
                sum += AccountPosting.GetSignedAmount(creditCardAccountId, paymentEvent);
            }
        }

        return sum;
    }

    private static IEnumerable<DateOnly> EnumerateClosingDates(
        CreditCardContract creditCard,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var current = new DateOnly(
            creditCard.StartDate.Year,
            creditCard.StartDate.Month,
            1);

        var endMonth = new DateOnly(
            simulationEnd.Year,
            simulationEnd.Month,
            1);

        while (current <= endMonth)
        {
            var closingDate = CreateClampedDate(
                current.Year,
                current.Month,
                creditCard.ClosingDayOfMonth);

            if (closingDate >= creditCard.StartDate &&
                closingDate >= simulationStart &&
                closingDate <= simulationEnd &&
                (creditCard.EndDate is null || closingDate <= creditCard.EndDate.Value))
            {
                yield return closingDate;
            }

            current = current.AddMonths(1);
        }
    }

    /// <summary>
    /// A payment always settles a statement that has already closed.
    /// When the payment day-of-month falls before the closing day-of-month the
    /// payment therefore belongs to the month AFTER the closing date -- taking it
    /// from the closing month would debit the bank account before the statement
    /// it pays even exists.
    /// </summary>
    private static DateOnly CreatePaymentDate(
        CreditCardContract creditCard,
        DateOnly closingDate)
    {
        var paymentMonth = creditCard.PaymentDayOfMonth <= creditCard.ClosingDayOfMonth
            ? closingDate.AddMonths(1)
            : closingDate;

        return CreateClampedDate(
            paymentMonth.Year,
            paymentMonth.Month,
            creditCard.PaymentDayOfMonth);
    }

    private static DateOnly CreateClampedDate(
        int year,
        int month,
        int day)
    {
        var daysInMonth = DateTime.DaysInMonth(year, month);
        return new DateOnly(year, month, Math.Min(day, daysInMonth));
    }

    private static DateOnly ApplyBusinessDayAdjustment(
        DateOnly date,
        BusinessDayAdjustment adjustment)
    {
        return adjustment switch
        {
            BusinessDayAdjustment.None => date,
            BusinessDayAdjustment.NextBusinessDay => MoveToNextBusinessDay(date),
            BusinessDayAdjustment.PreviousBusinessDay => MoveToPreviousBusinessDay(date),
            _ => date
        };
    }

    private static DateOnly MoveToNextBusinessDay(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static DateOnly MoveToPreviousBusinessDay(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(-1);
        }

        return date;
    }

    private static PaymentMethod MapPaymentMethod(
        CreditCardPaymentMethod method)
    {
        return method switch
        {
            CreditCardPaymentMethod.AutomaticLsv => PaymentMethod.Lsv,
            CreditCardPaymentMethod.ManualBankTransfer => PaymentMethod.BankTransfer,
            _ => PaymentMethod.Unknown
        };
    }

    private readonly record struct DatedAmount(
        DateOnly Date,
        decimal Amount);

    /// <summary>
    /// Every posting from the base events that hits one credit-card account,
    /// ordered by date, with prefix sums.
    /// </summary>
    private sealed class CardAccountLedger
    {
        private static readonly CardAccountLedger EmptyLedger = new([], [0m]);

        private readonly DateOnly[] _dates;

        /// <summary>
        /// <c>_prefixSums[i]</c> is the sum of the first <c>i</c> postings, so it
        /// has one more entry than <see cref="_dates"/>.
        /// </summary>
        private readonly decimal[] _prefixSums;

        private CardAccountLedger(DateOnly[] dates, decimal[] prefixSums)
        {
            _dates = dates;
            _prefixSums = prefixSums;
        }

        public static CardAccountLedger Build(
            Guid creditCardAccountId,
            IReadOnlyList<CashFlowEvent> baseEvents)
        {
            var postings = new List<DatedAmount>();

            foreach (var cashFlowEvent in baseEvents)
            {
                var amount = AccountPosting.GetSignedAmount(creditCardAccountId, cashFlowEvent);

                if (amount != 0m)
                {
                    postings.Add(new DatedAmount(cashFlowEvent.Date, amount));
                }
            }

            if (postings.Count == 0)
            {
                return EmptyLedger;
            }

            var ordered = postings.ToArray();

            // Only the sum up to a date boundary is ever read and adding decimals
            // is exact and order independent, so an unstable sort by date is safe
            // even though the caller does not promise sorted input.
            Array.Sort(ordered, static (left, right) => left.Date.CompareTo(right.Date));

            var dates = new DateOnly[ordered.Length];
            var prefixSums = new decimal[ordered.Length + 1];

            for (var i = 0; i < ordered.Length; i++)
            {
                dates[i] = ordered[i].Date;
                prefixSums[i + 1] = prefixSums[i] + ordered[i].Amount;
            }

            return new CardAccountLedger(dates, prefixSums);
        }

        /// <summary>
        /// Sum of every posting dated on or before <paramref name="closingDate"/>.
        /// <paramref name="cursor"/> is the caller's position and must only ever
        /// be called with non-decreasing dates.
        /// </summary>
        public decimal SumUpToAndIncluding(DateOnly closingDate, ref int cursor)
        {
            while (cursor < _dates.Length && _dates[cursor] <= closingDate)
            {
                cursor++;
            }

            return _prefixSums[cursor];
        }
    }
}