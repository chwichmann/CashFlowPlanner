namespace CashFlowPlanner.Core.Analysis;

public static class PersonIncomeSummaryCalculator
{
    public static IReadOnlyDictionary<Guid, decimal> CalculateIncomeByPerson(
        CashFlowPlan plan,
        int year)
    {
        var result = plan.Persons.ToDictionary(
            person => person.Id,
            _ => 0m);

        foreach (var transaction in plan.Transactions)
        {
            if (!transaction.IsActive)
            {
                continue;
            }

            if (transaction.IncomePersonId is null)
            {
                continue;
            }

            if (!result.ContainsKey(transaction.IncomePersonId.Value))
            {
                continue;
            }

            var occurrences = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
                transaction.Schedule,
                year);

            result[transaction.IncomePersonId.Value] += transaction.Amount * occurrences;
        }

        return result;
    }
}