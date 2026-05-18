namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aTaxYearSimulator
{
    private readonly ScheduleOccurrenceGenerator _scheduleOccurrenceGenerator;

    public Pillar3aTaxYearSimulator()
        : this(new ScheduleOccurrenceGenerator())
    {
    }

    public Pillar3aTaxYearSimulator(
        ScheduleOccurrenceGenerator scheduleOccurrenceGenerator)
    {
        _scheduleOccurrenceGenerator = scheduleOccurrenceGenerator;
    }

    public Pillar3aTaxYearSimulationResult Simulate(
        CashFlowPlan plan,
        int taxYear,
        decimal annualLimitPerPerson)
    {
        if (annualLimitPerPerson < 0m)
        {
            throw new InvalidOperationException("Pillar 3a annual limit must not be negative.");
        }

        plan.Validate();

        var yearStart = new DateOnly(taxYear, 1, 1);
        var yearEnd = new DateOnly(taxYear, 12, 31);

        var activeContracts = plan.Pillar3aContracts
            .Where(x => x.IsActive)
            .ToList();

        var rows = activeContracts
            .GroupBy(x => x.OwnerPersonId)
            .Select(group =>
            {
                var scheduledContributions = 0m;
                var activeScheduleCount = 0;

                foreach (var contract in group)
                {
                    foreach (var schedule in contract.ContributionSchedules.Where(x => x.IsActive))
                    {
                        schedule.Validate(contract.Name);

                        activeScheduleCount++;

                        var occurrences = _scheduleOccurrenceGenerator.GenerateOccurrences(
                            schedule.ToSchedule(),
                            yearStart,
                            yearEnd);

                        scheduledContributions += occurrences.Count * schedule.Amount;
                    }
                }

                var remaining = Math.Max(0m, annualLimitPerPerson - scheduledContributions);
                var excess = Math.Max(0m, scheduledContributions - annualLimitPerPerson);

                return new Pillar3aTaxYearSimulationPersonResult
                {
                    PersonId = group.Key,
                    TaxYear = taxYear,
                    AnnualLimit = annualLimitPerPerson,
                    ScheduledContributions = scheduledContributions,
                    Remaining = remaining,
                    Excess = excess,
                    IsLimitReached = scheduledContributions >= annualLimitPerPerson,
                    IsExceeded = scheduledContributions > annualLimitPerPerson,
                    ContractCount = group.Count(),
                    ActiveScheduleCount = activeScheduleCount
                };
            })
            .OrderBy(x => x.PersonId)
            .ToList();

        return new Pillar3aTaxYearSimulationResult
        {
            TaxYear = taxYear,
            AnnualLimitPerPerson = annualLimitPerPerson,
            Persons = rows
        };
    }
}