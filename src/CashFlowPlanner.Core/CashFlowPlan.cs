using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core;

public sealed class CashFlowPlan
{
    public Guid Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string BaseCurrency { get; init; } = "CHF";

    public List<Person> Persons { get; init; } = [];

    public List<Account> Accounts { get; init; } = [];

    public List<TransactionDefinition> Transactions { get; init; } = [];

    public List<MortgageContract> Mortgages { get; init; } = [];

    public List<CreditCardContract> CreditCards { get; init; } = [];

    // ✅ NEW
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

        var accountIds = Accounts.Select(x => x.Id).ToHashSet();
        var personIds = Persons.Select(x => x.Id).ToHashSet();

        foreach (var mortgage in Mortgages)
        {
            mortgage.Validate();
        }

        foreach (var creditCard in CreditCards)
        {
            creditCard.Validate();
        }

        // ✅ NEW
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
}