using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;

namespace CashFlowPlanner.Core.Validation;

public static class AccountValidator
{
    public static IReadOnlyList<PlanValidationMessage> Validate(
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<Person> persons)
    {
        var messages = new List<PlanValidationMessage>();
        var personIds = persons.Select(p => p.Id).ToHashSet();

        foreach (var account in accounts)
        {
            ValidateOwners(account, personIds, messages);
            ValidatePillar3a(account, messages);
        }

        return messages;
    }

    private static void ValidateOwners(
        Account account,
        HashSet<Guid> personIds,
        List<PlanValidationMessage> messages)
    {
        foreach (var owner in account.Owners)
        {
            if (!personIds.Contains(owner.PersonId))
            {
                messages.Add(new PlanValidationMessage
                {
                    Severity = PlanValidationSeverity.Error,
                    Code = "AccountOwner.PersonNotFound",
                    EntityId = account.Id,
                    Message = $"Account '{account.Name}' references a person that does not exist."
                });
            }

            if (owner.OwnershipShare <= 0m || owner.OwnershipShare > 1m)
            {
                messages.Add(new PlanValidationMessage
                {
                    Severity = PlanValidationSeverity.Error,
                    Code = "AccountOwner.InvalidShare",
                    EntityId = account.Id,
                    Message = $"Account '{account.Name}' has an invalid ownership share."
                });
            }
        }
    }

    private static void ValidatePillar3a(
        Account account,
        List<PlanValidationMessage> messages)
    {
        if (account.Type != AccountType.Pillar3a)
        {
            if (account.Pillar3aSubtype is not null)
            {
                messages.Add(new PlanValidationMessage
                {
                    Severity = PlanValidationSeverity.Warning,
                    Code = "Account.Pillar3aSubtypeOnNonPillar3a",
                    EntityId = account.Id,
                    Message = $"Account '{account.Name}' has a Pillar 3a subtype but is not a Pillar 3a account."
                });
            }

            return;
        }

        if (account.Owners.Count != 1)
        {
            messages.Add(new PlanValidationMessage
            {
                Severity = PlanValidationSeverity.Error,
                Code = "Pillar3aAccount.InvalidOwnerCount",
                EntityId = account.Id,
                Message = $"Pillar 3a account '{account.Name}' must have exactly one owner."
            });
        }

        if (account.Pillar3aSubtype is null)
        {
            messages.Add(new PlanValidationMessage
            {
                Severity = PlanValidationSeverity.Error,
                Code = "Pillar3aAccount.MissingSubtype",
                EntityId = account.Id,
                Message = $"Pillar 3a account '{account.Name}' must define whether it is a bank account or fund solution."
            });
        }
    }
}