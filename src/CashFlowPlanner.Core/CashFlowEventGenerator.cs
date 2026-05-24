using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core;

public sealed class CashFlowEventGenerator
{
    private readonly ScheduleOccurrenceGenerator _scheduleOccurrenceGenerator;
    private readonly AccountInterestEventGenerator _accountInterestEventGenerator;

    public CashFlowEventGenerator()
        : this(
            new ScheduleOccurrenceGenerator(),
            new AccountInterestEventGenerator())
    {
    }

    public CashFlowEventGenerator(
        ScheduleOccurrenceGenerator scheduleOccurrenceGenerator)
        : this(
            scheduleOccurrenceGenerator,
            new AccountInterestEventGenerator())
    {
    }

    public CashFlowEventGenerator(
        ScheduleOccurrenceGenerator scheduleOccurrenceGenerator,
        AccountInterestEventGenerator accountInterestEventGenerator)
    {
        _scheduleOccurrenceGenerator = scheduleOccurrenceGenerator;
        _accountInterestEventGenerator = accountInterestEventGenerator;
    }

    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IEnumerable<TransactionDefinition> transactions,
        DateOnly startDate,
        DateOnly endDate)
    {
        var events = GenerateTransactionEvents(
            transactions,
            startDate,
            endDate,
            plan: null);

        return SortEvents(events);
    }

    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        CashFlowPlan plan)
    {
        return GenerateEvents(
            plan,
            plan.SimulationSettings.StartDate,
            plan.SimulationSettings.EndDate);
    }

    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        CashFlowPlan plan,
        DateOnly startDate,
        DateOnly endDate)
    {
        plan.Validate();

        var events = GenerateTransactionEvents(
            plan.Transactions,
            startDate,
            endDate,
            plan);

        var hasInterestContracts = plan.Accounts.Any(account =>
            account.InterestContracts.Any(contract => contract.IsActive));

        if (hasInterestContracts)
        {
            var interestEvents = _accountInterestEventGenerator.GenerateEvents(
                plan.Accounts,
                events,
                startDate,
                endDate);

            events.AddRange(interestEvents);
        }

        return SortEvents(events);
    }

    private List<CashFlowEvent> GenerateTransactionEvents(
        IEnumerable<TransactionDefinition> transactions,
        DateOnly startDate,
        DateOnly endDate,
        CashFlowPlan? plan)
    {
        var events = new List<CashFlowEvent>();

        foreach (var transaction in transactions.Where(x => x.IsActive))
        {
            transaction.Validate();

            var dates = _scheduleOccurrenceGenerator.GenerateOccurrences(
                transaction.Schedule,
                startDate,
                endDate,
                plan);

            foreach (var date in dates)
            {
                events.Add(new CashFlowEvent
                {
                    SourceTransactionId = transaction.Id,
                    Name = transaction.Name,
                    Date = date,
                    Kind = transaction.Kind,
                    FromAccountId = transaction.FromAccountId,
                    ToAccountId = transaction.ToAccountId,
                    Amount = transaction.Amount,
                    Currency = transaction.Currency,
                    Priority = transaction.Priority,
                    Category = transaction.Category,
                    Counterparty = transaction.Counterparty,
                    PaymentMethod = transaction.PaymentMethod,
                    Notes = transaction.Notes
                });
            }
        }

        return events;
    }

    private static IReadOnlyList<CashFlowEvent> SortEvents(
        IEnumerable<CashFlowEvent> events)
    {
        return events
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }
}