using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.Core;

/// <summary>
/// Turns transaction definitions into dated cash-flow events.
/// This type deliberately knows nothing about account interest: interest depends
/// on the running balance, so it has to be generated exactly once and last, by
/// <see cref="SimulationEngine"/>, after credit-card payments are known.
/// </summary>
public sealed class CashFlowEventGenerator
{
    private readonly ScheduleOccurrenceGenerator _scheduleOccurrenceGenerator;

    public CashFlowEventGenerator()
        : this(new ScheduleOccurrenceGenerator())
    {
    }

    public CashFlowEventGenerator(
        ScheduleOccurrenceGenerator scheduleOccurrenceGenerator)
    {
        _scheduleOccurrenceGenerator = scheduleOccurrenceGenerator;
    }

    /// <summary>
    /// Generates events with no indexation. Callers that have a plan should use
    /// the plan-aware overload so the plan's inflation assumption applies.
    /// </summary>
    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IEnumerable<TransactionDefinition> transactions,
        DateOnly startDate,
        DateOnly endDate)
    {
        return GenerateEvents(
            transactions,
            startDate,
            endDate,
            inflation: null);
    }

    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IEnumerable<TransactionDefinition> transactions,
        DateOnly startDate,
        DateOnly endDate,
        InflationAssumption? inflation)
    {
        var events = GenerateTransactionEvents(
            transactions,
            startDate,
            endDate,
            plan: null,
            inflation);

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
            plan,
            plan.Inflation);

        return SortEvents(events);
    }

    private List<CashFlowEvent> GenerateTransactionEvents(
        IEnumerable<TransactionDefinition> transactions,
        DateOnly startDate,
        DateOnly endDate,
        CashFlowPlan? plan,
        InflationAssumption? inflation)
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

            var indexation = TransactionIndexer.Resolve(transaction, inflation);

            foreach (var date in dates)
            {
                // Indexation compounds on the anniversary of the base date, not
                // per occurrence: all twelve charges of an indexation year carry
                // the same amount, and the amount steps once a year.
                var factor = indexation is null
                    ? 1m
                    : AnnualCompounding.Factor(
                        indexation.Value.RatePercent,
                        indexation.Value.BaseDate,
                        date);

                events.Add(new CashFlowEvent
                {
                    SourceTransactionId = transaction.Id,
                    Name = transaction.Name,
                    Date = date,
                    Kind = transaction.Kind,
                    FromAccountId = transaction.FromAccountId,
                    ToAccountId = transaction.ToAccountId,
                    Amount = factor == 1m
                        ? transaction.Amount
                        : transaction.Amount * factor,
                    IndexationFactor = factor,
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