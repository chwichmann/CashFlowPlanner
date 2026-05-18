using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Analysis;

namespace CashFlowPlanner.Core.Pillar3a;

public static class Pillar3aContributionCalculator
{
    public static IReadOnlyList<Pillar3aContributionSummary> Calculate(
        CashFlowPlan plan,
        int year,
        Pillar3aLimitRule limitRule)
    {
        if (limitRule.Year != year)
        {
            throw new InvalidOperationException(
                $"The Pillar 3a limit rule is for {limitRule.Year}, but calculation was requested for {year}.");
        }

        var personsById = plan.Persons.ToDictionary(x => x.Id);

        var summaries = plan.Persons.ToDictionary(
            person => person.Id,
            person => new Pillar3aContributionSummary
            {
                PersonId = person.Id,
                Year = year,
                MaxAllowed = limitRule.MaxContributionPerPerson,
                Contributions = 0m
            });

        var pillar3aAccountsById = plan.Accounts
            .Where(account => account.Type == AccountType.Pillar3a)
            .ToDictionary(account => account.Id);

        foreach (var transaction in plan.Transactions)
        {
            if (!transaction.IsActive)
            {
                continue;
            }

            if (transaction.ToAccountId is null)
            {
                continue;
            }

            if (!pillar3aAccountsById.TryGetValue(transaction.ToAccountId.Value, out var targetAccount))
            {
                continue;
            }

            var owner = targetAccount.Owners.SingleOrDefault();

            if (owner is null)
            {
                continue;
            }

            if (!summaries.TryGetValue(owner.PersonId, out var summary))
            {
                continue;
            }

            var occurrences = ScheduledTransactionYearCalculator.CountOccurrencesInYear(
                transaction.Schedule,
                year);

            summary.Contributions += transaction.Amount * occurrences;
        }

        return summaries.Values
            .OrderBy(x => personsById[x.PersonId].DisplayName)
            .ToList();
    }
}