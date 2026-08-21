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

    private static IReadOnlyList<CashFlowEvent> GenerateEventsForCreditCard(
        CreditCardContract creditCard,
        Account creditCardAccount,
        IReadOnlyList<CashFlowEvent> baseEvents,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var generatedPaymentEvents = new List<CashFlowEvent>();

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

            var knownEvents = baseEvents
                .Concat(generatedPaymentEvents)
                .OrderBy(x => x.Date)
                .ThenBy(x => x.Priority)
                .ThenBy(x => x.Name)
                .ToList();

            var balanceAtClosing = CalculateCreditCardBalanceAtDate(
                creditCardAccount,
                creditCard.CreditCardAccountId,
                knownEvents,
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

    private static decimal CalculateCreditCardBalanceAtDate(
        Account creditCardAccount,
        Guid creditCardAccountId,
        IReadOnlyList<CashFlowEvent> events,
        DateOnly closingDate)
    {
        var balance = creditCardAccount.OpeningDate <= closingDate
            ? creditCardAccount.OpeningBalance
            : 0m;

        var relevantEvents = events
            .Where(x => x.Date <= closingDate)
            .Where(x => x.FromAccountId == creditCardAccountId || x.ToAccountId == creditCardAccountId)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name);

        foreach (var cashFlowEvent in relevantEvents)
        {
            balance += AccountPosting.GetSignedAmount(creditCardAccountId, cashFlowEvent);
        }

        return balance;
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
}