namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aEventGenerator
{
    private readonly CashFlowEventGenerator _eventGenerator;

    public Pillar3aEventGenerator()
        : this(new CashFlowEventGenerator())
    {
    }

    public Pillar3aEventGenerator(CashFlowEventGenerator eventGenerator)
    {
        _eventGenerator = eventGenerator;
    }

    public IReadOnlyList<CashFlowEvent> GenerateEvents(
        IReadOnlyCollection<Pillar3aContract> contracts,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var events = new List<CashFlowEvent>();

        foreach (var contract in contracts.Where(x => x.IsActive))
        {
            contract.Validate();

            foreach (var schedule in contract.ContributionSchedules.Where(x => x.IsActive))
            {
                schedule.Validate(contract.Name);

                var transaction = CreateTransactionDefinition(
                    contract,
                    schedule);

                var generatedEvents = _eventGenerator.GenerateEvents(
                    [transaction],
                    simulationStart,
                    simulationEnd);

                events.AddRange(generatedEvents);
            }
        }

        return events
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private static TransactionDefinition CreateTransactionDefinition(
        Pillar3aContract contract,
        Pillar3aContributionSchedule schedule)
    {
        return new TransactionDefinition
        {
            // Use the contract as source, because this event is generated
            // from the Pillar 3a contract, not from a normal user transaction.
            Id = contract.Id,

            Name = $"{contract.Name} contribution",

            Kind = TransactionKind.ExternalExpense,

            FromAccountId = schedule.PaymentAccountId,
            ToAccountId = null,

            Amount = schedule.Amount,
            Currency = schedule.Currency,

            Schedule = schedule.ToSchedule(),

            Category = "Pillar 3a Contribution",
            Counterparty = string.IsNullOrWhiteSpace(contract.ProviderName)
                ? contract.Name
                : contract.ProviderName,

            PaymentMethod = PaymentMethod.BankTransfer,

            Priority = 45,

            IsActive = schedule.IsActive,

            Notes = "Generated from Pillar 3a contract."
        };
    }
}