using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;
using CashFlowPlanner.Core.Validation;

namespace CashFlowPlanner.Core;

public sealed class CashFlowPlan
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string BaseCurrency { get; init; } = "CHF";

    public Guid? DefaultPaymentAccountId { get; init; }

    public bool TreatWeekendsAsBankOffDays { get; init; } = true;

    public List<BankOffDay> BankOffDays { get; init; } = [];

    public List<Person> Persons { get; init; } = [];

    public List<Account> Accounts { get; init; } = [];

    public List<TransactionDefinition> Transactions { get; init; } = [];

    public List<MortgageContract> Mortgages { get; init; } = [];

    public List<CreditCardContract> CreditCards { get; init; } = [];

    public List<Pillar3aContract> Pillar3aContracts { get; init; } = [];

    public SimulationSettings SimulationSettings { get; init; } = new();

    public List<HouseBuySimulatorScenario> HouseBuyScenarios { get; init; } = [];

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("Plan Id must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Plan name is required.");
        }

        if (string.IsNullOrWhiteSpace(BaseCurrency))
        {
            throw new InvalidOperationException("Base currency is required.");
        }

        AssertUniqueIds("account", Accounts.Select(x => x.Id));
        AssertUniqueIds("transaction", Transactions.Select(x => x.Id));
        AssertUniqueIds("person", Persons.Select(x => x.Id));
        AssertUniqueIds("mortgage", Mortgages.Select(x => x.Id));
        AssertUniqueIds("credit card", CreditCards.Select(x => x.Id));
        AssertUniqueIds("Pillar 3a contract", Pillar3aContracts.Select(x => x.Id));

        var accountIds = Accounts.Select(x => x.Id).ToHashSet();
        var personIds = Persons.Select(x => x.Id).ToHashSet();

        if (DefaultPaymentAccountId is not null &&
            !accountIds.Contains(DefaultPaymentAccountId.Value))
        {
            throw new InvalidOperationException(
                $"Default payment account references unknown account '{DefaultPaymentAccountId}'.");
        }

        ValidateBankOffDays();
        ValidateAccounts();

        foreach (var transaction in Transactions)
        {
            transaction.Validate();

            AssertAccountExists(
                accountIds,
                transaction.FromAccountId,
                $"Transaction '{transaction.Name}'",
                "source account");

            AssertAccountExists(
                accountIds,
                transaction.ToAccountId,
                $"Transaction '{transaction.Name}'",
                "target account");
        }

        foreach (var mortgage in Mortgages)
        {
            mortgage.Validate();

            AssertAccountExists(
                accountIds,
                mortgage.PaymentAccountId,
                $"Mortgage '{mortgage.Name}'",
                "payment account");

            AssertAccountExists(
                accountIds,
                mortgage.IndirectAmortisationAccountId,
                $"Mortgage '{mortgage.Name}'",
                "indirect amortisation account");
        }

        foreach (var creditCard in CreditCards)
        {
            creditCard.Validate();

            AssertAccountExists(
                accountIds,
                creditCard.CreditCardAccountId,
                $"Credit card contract '{creditCard.Name}'",
                "credit card account");

            AssertAccountExists(
                accountIds,
                creditCard.PaymentAccountId,
                $"Credit card contract '{creditCard.Name}'",
                "payment account");
        }

        foreach (var pillar3a in Pillar3aContracts)
        {
            pillar3a.Validate();

            if (!personIds.Contains(pillar3a.OwnerPersonId))
            {
                throw new InvalidOperationException(
                    $"Pillar 3a contract '{pillar3a.Name}' references unknown person '{pillar3a.OwnerPersonId}'.");
            }

            foreach (var schedule in pillar3a.ContributionSchedules)
            {
                if (!accountIds.Contains(schedule.PaymentAccountId))
                {
                    throw new InvalidOperationException(
                        $"Pillar 3a contract '{pillar3a.Name}' references unknown payment account '{schedule.PaymentAccountId}'.");
                }
            }

            foreach (var withdrawal in pillar3a.Withdrawals)
            {
                if (withdrawal.TargetAccountId is not null &&
                    !accountIds.Contains(withdrawal.TargetAccountId.Value))
                {
                    throw new InvalidOperationException(
                        $"Pillar 3a contract '{pillar3a.Name}' references unknown withdrawal target account '{withdrawal.TargetAccountId}'.");
                }
            }

            if (!string.Equals(pillar3a.Currency, BaseCurrency, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Pillar 3a contract '{pillar3a.Name}' uses unsupported currency '{pillar3a.Currency}'.");
            }
        }

        SimulationSettings.Validate();
    }

    /// <summary>
    /// Duplicate Ids used to load cleanly and then break every lookup that assumes
    /// a single match -- deleting an account by Id threw instead of deleting.
    /// </summary>
    private static void AssertUniqueIds(
        string entityName,
        IEnumerable<Guid> ids)
    {
        var duplicates = ids
            .GroupBy(x => x)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        if (duplicates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Plan contains duplicate {entityName} ids: {string.Join(", ", duplicates)}.");
        }
    }

    private static void AssertAccountExists(
        HashSet<Guid> accountIds,
        Guid? accountId,
        string ownerDescription,
        string roleDescription)
    {
        if (accountId is null || accountId.Value == Guid.Empty)
        {
            return;
        }

        if (!accountIds.Contains(accountId.Value))
        {
            throw new InvalidOperationException(
                $"{ownerDescription} references unknown {roleDescription} '{accountId.Value}'.");
        }
    }

    /// <summary>
    /// Runs <see cref="AccountValidator"/>, which was complete but called from
    /// nowhere. Errors abort the plan; warnings are advisory and do not.
    /// </summary>
    private void ValidateAccounts()
    {
        var messages = AccountValidator.Validate(Accounts, Persons);

        var errors = messages
            .Where(x => x.Severity == PlanValidationSeverity.Error)
            .ToList();

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                string.Join(" ", errors.Select(x => x.Message)));
        }
    }

    private void ValidateBankOffDays()
    {
        var duplicateDates = BankOffDays
            .GroupBy(x => x.Date)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x)
            .ToList();

        if (duplicateDates.Count > 0)
        {
            throw new InvalidOperationException(
                $"Bank off-days contain duplicate dates: {string.Join(", ", duplicateDates)}.");
        }

        foreach (var offDay in BankOffDays)
        {
            offDay.Validate();
        }
    }
}
