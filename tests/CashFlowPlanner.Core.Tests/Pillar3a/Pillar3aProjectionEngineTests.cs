namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aProjectionEngine
{
    private readonly ScheduleOccurrenceGenerator _scheduleOccurrenceGenerator;

    public Pillar3aProjectionEngine()
        : this(new ScheduleOccurrenceGenerator())
    {
    }

    public Pillar3aProjectionEngine(ScheduleOccurrenceGenerator scheduleOccurrenceGenerator)
    {
        _scheduleOccurrenceGenerator = scheduleOccurrenceGenerator;
    }

    public IReadOnlyList<Pillar3aProjectionResult> Project(
        CashFlowPlan plan,
        DateOnly today)
    {
        plan.Validate();

        var results = new List<Pillar3aProjectionResult>();

        foreach (var contract in plan.Pillar3aContracts.Where(x => x.IsActive))
        {
            contract.Validate();

            var person = plan.Persons.SingleOrDefault(x => x.Id == contract.OwnerPersonId);

            if (person is null)
            {
                throw new InvalidOperationException(
                    $"Pillar 3a contract '{contract.Name}' references unknown owner person '{contract.OwnerPersonId}'.");
            }

            var projectionStart = today < contract.OpeningDate
                ? contract.OpeningDate
                : today;

            var projectionEnd = person.RetirementDate
                ?? projectionStart.AddYears(30);

            if (projectionEnd < projectionStart)
            {
                results.Add(new Pillar3aProjectionResult
                {
                    ContractId = contract.Id,
                    ContractName = contract.Name,
                    OwnerPersonId = contract.OwnerPersonId,
                    Points = []
                });

                continue;
            }

            var points = ProjectContract(
                contract,
                projectionStart,
                projectionEnd);

            results.Add(new Pillar3aProjectionResult
            {
                ContractId = contract.Id,
                ContractName = contract.Name,
                OwnerPersonId = contract.OwnerPersonId,
                Points = points
            });
        }

        return results;
    }

    private IReadOnlyList<Pillar3aProjectionPoint> ProjectContract(
        Pillar3aContract contract,
        DateOnly startDate,
        DateOnly endDate)
    {
        var contributionDates = GenerateContributionDates(
            contract,
            startDate,
            endDate);

        var withdrawals = contract.Withdrawals
            .OrderBy(x => x.Date)
            .ToList();

        var points = new List<Pillar3aProjectionPoint>();

        var currentValue = contract.OpeningValue;

        var currentMonth = new DateOnly(startDate.Year, startDate.Month, 1);

        while (currentMonth <= endDate)
        {
            var monthStart = currentMonth;
            var monthEnd = EndOfMonth(currentMonth);

            if (monthEnd < startDate)
            {
                currentMonth = currentMonth.AddMonths(1);
                continue;
            }

            if (monthStart > endDate)
            {
                break;
            }

            var effectiveMonthStart = monthStart < startDate
                ? startDate
                : monthStart;

            var effectiveMonthEnd = monthEnd > endDate
                ? endDate
                : monthEnd;

            var contributions = contributionDates
                .Where(x =>
                    x.Date >= effectiveMonthStart &&
                    x.Date <= effectiveMonthEnd)
                .Sum(x => x.Amount);

            var monthWithdrawals = CalculateWithdrawalsForMonth(
                contract,
                withdrawals,
                effectiveMonthStart,
                effectiveMonthEnd,
                currentValue,
                out var closesContract);

            var growth = CalculateGrowth(
                contract,
                currentValue,
                effectiveMonthStart,
                effectiveMonthEnd.AddDays(1));

            currentValue += contributions;
            currentValue += growth;
            currentValue -= monthWithdrawals;

            if (currentValue < 0m)
            {
                currentValue = 0m;
            }

            if (closesContract)
            {
                currentValue = 0m;
            }

            points.Add(new Pillar3aProjectionPoint
            {
                Date = effectiveMonthEnd,
                Contributions = Math.Round(contributions, 2, MidpointRounding.AwayFromZero),
                Growth = Math.Round(growth, 2, MidpointRounding.AwayFromZero),
                Withdrawals = Math.Round(monthWithdrawals, 2, MidpointRounding.AwayFromZero),
                Value = Math.Round(currentValue, 2, MidpointRounding.AwayFromZero),
                Currency = contract.Currency
            });

            if (closesContract)
            {
                break;
            }

            currentMonth = currentMonth.AddMonths(1);
        }

        return points;
    }

    private IReadOnlyList<ContributionOccurrence> GenerateContributionDates(
        Pillar3aContract contract,
        DateOnly startDate,
        DateOnly endDate)
    {
        var result = new List<ContributionOccurrence>();

        foreach (var contributionSchedule in contract.ContributionSchedules.Where(x => x.IsActive))
        {
            contributionSchedule.Validate(contract.Name);

            var schedule = contributionSchedule.ToSchedule();

            var occurrences = _scheduleOccurrenceGenerator.GenerateOccurrences(
                schedule,
                startDate,
                endDate);

            foreach (var occurrence in occurrences)
            {
                result.Add(new ContributionOccurrence(
                    occurrence,
                    contributionSchedule.Amount));
            }
        }

        return result
            .OrderBy(x => x.Date)
            .ToList();
    }

    private static decimal CalculateWithdrawalsForMonth(
        Pillar3aContract contract,
        IReadOnlyCollection<Pillar3aWithdrawalEvent> withdrawals,
        DateOnly monthStart,
        DateOnly monthEnd,
        decimal currentValue,
        out bool closesContract)
    {
        closesContract = false;
        var total = 0m;

        foreach (var withdrawal in withdrawals.Where(x => x.Date >= monthStart && x.Date <= monthEnd))
        {
            withdrawal.Validate(contract.Name);

            if (withdrawal.CloseContract)
            {
                total += currentValue;
                closesContract = true;
                continue;
            }

            total += withdrawal.Amount ?? 0m;
        }

        return total;
    }

    private static decimal CalculateGrowth(
        Pillar3aContract contract,
        decimal currentValue,
        DateOnly periodStart,
        DateOnly periodEndExclusive)
    {
        if (currentValue <= 0m)
        {
            return 0m;
        }

        if (periodEndExclusive <= periodStart)
        {
            return 0m;
        }

        var days = periodEndExclusive.DayNumber - periodStart.DayNumber;

        if (days <= 0)
        {
            return 0m;
        }

        var assumption = contract.ProjectionAssumption;

        return assumption.Method switch
        {
            Pillar3aProjectionMethod.FixedInterest =>
                currentValue *
                (assumption.ExpectedAnnualReturnPercent / 100m) *
                days / 365m,

            Pillar3aProjectionMethod.ExpectedReturn =>
                currentValue *
                ((assumption.ExpectedAnnualReturnPercent - assumption.AnnualFeePercent) / 100m) *
                days / 365m,

            Pillar3aProjectionMethod.InsuranceGuaranteedPayout =>
                0m,

            Pillar3aProjectionMethod.None =>
                0m,

            _ =>
                0m
        };
    }

    private static DateOnly EndOfMonth(DateOnly date)
    {
        return new DateOnly(
            date.Year,
            date.Month,
            DateTime.DaysInMonth(date.Year, date.Month));
    }

    private readonly record struct ContributionOccurrence(
        DateOnly Date,
        decimal Amount);
}