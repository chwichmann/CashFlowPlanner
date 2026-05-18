using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class CashFlowAppState
{
    public CashFlowPlanDocument? CurrentDocument { get; private set; }

    public CashFlowPlan? CurrentPlan { get; private set; }

    public SimulationResult? CurrentSimulationResult { get; private set; }

    public bool HasPlan => CurrentPlan is not null;

    public event Action? Changed;

    public void LoadDocument(CashFlowPlanDocument document)
    {
        CurrentDocument = document;
        CurrentPlan = document.ToPlan();
        CurrentSimulationResult = null;

        NotifyChanged();
    }

    public void SetPlan(CashFlowPlan plan)
    {
        CurrentPlan = plan;
        CurrentDocument = plan.ToDocument(CurrentDocument);
        CurrentSimulationResult = null;

        NotifyChanged();
    }

    public SimulationResult RunSimulation()
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var engine = new SimulationEngine();

        CurrentSimulationResult = engine.Simulate(CurrentPlan);

        NotifyChanged();

        return CurrentSimulationResult;
    }

    public CashFlowPlanDocument GetDocumentForSave()
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        return CurrentDocument;
    }

    public void AddOrUpdateAccount(Account account)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var existingIndex = CurrentPlan.Accounts.FindIndex(x => x.Id == account.Id);

        if (existingIndex >= 0)
        {
            CurrentPlan.Accounts[existingIndex] = account;
        }
        else
        {
            CurrentPlan.Accounts.Add(account);
        }

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeleteAccount(Guid accountId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var isUsedByTransaction = CurrentPlan.Transactions.Any(x =>
            x.FromAccountId == accountId ||
            x.ToAccountId == accountId);

        if (isUsedByTransaction)
        {
            throw new InvalidOperationException(
                "The account cannot be deleted because it is used by one or more transactions.");
        }

        var account = CurrentPlan.Accounts.SingleOrDefault(x => x.Id == accountId);

        if (account is null)
        {
            return;
        }

        CurrentPlan.Accounts.Remove(account);

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdateTransaction(TransactionDefinition transaction)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        transaction.Validate();

        var existingIndex = CurrentPlan.Transactions.FindIndex(x => x.Id == transaction.Id);

        if (existingIndex >= 0)
        {
            CurrentPlan.Transactions[existingIndex] = transaction;
        }
        else
        {
            CurrentPlan.Transactions.Add(transaction);
        }

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeleteTransaction(Guid transactionId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var transaction = CurrentPlan.Transactions.SingleOrDefault(x => x.Id == transactionId);

        if (transaction is null)
        {
            return;
        }

        CurrentPlan.Transactions.Remove(transaction);

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void UpdateSimulationSettings(SimulationSettings settings)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        settings.Validate();

        CurrentPlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = settings
        };

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdateMortgage(MortgageContract mortgage)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        mortgage.Validate();

        var mortgages = CurrentPlan.Mortgages.ToList();

        var existingIndex = mortgages.FindIndex(x => x.Id == mortgage.Id);

        if (existingIndex >= 0)
        {
            mortgages[existingIndex] = mortgage;
        }
        else
        {
            mortgages.Add(mortgage);
        }

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeleteMortgage(Guid mortgageId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var mortgages = CurrentPlan.Mortgages
            .Where(x => x.Id != mortgageId)
            .ToList();

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdateCreditCard(CreditCardContract creditCard)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        creditCard.Validate();

        var creditCards = CurrentPlan.CreditCards.ToList();

        var existingIndex = creditCards.FindIndex(x => x.Id == creditCard.Id);

        if (existingIndex >= 0)
        {
            creditCards[existingIndex] = creditCard;
        }
        else
        {
            creditCards.Add(creditCard);
        }

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = creditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeleteCreditCard(Guid creditCardId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var creditCards = CurrentPlan.CreditCards
            .Where(x => x.Id != creditCardId)
            .ToList();

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = creditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdatePerson(Person person)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        if (string.IsNullOrWhiteSpace(person.DisplayName))
        {
            throw new InvalidOperationException("The person display name is required.");
        }

        var persons = CurrentPlan.Persons.ToList();

        var existingIndex = persons.FindIndex(x => x.Id == person.Id);

        if (existingIndex >= 0)
        {
            persons[existingIndex] = person;
        }
        else
        {
            persons.Add(person);
        }

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeletePerson(Guid personId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var isUsedByAccount = CurrentPlan.Accounts.Any(account =>
            account.Owners.Any(owner => owner.PersonId == personId));

        if (isUsedByAccount)
        {
            throw new InvalidOperationException(
                "The person cannot be deleted because it is used as owner of one or more accounts.");
        }

        var isUsedByTransaction = CurrentPlan.Transactions.Any(transaction =>
            transaction.IncomePersonId == personId);

        if (isUsedByTransaction)
        {
            throw new InvalidOperationException(
                "The person cannot be deleted because it is used by one or more income transactions.");
        }

        var isUsedByPillar3aContract = CurrentPlan.Pillar3aContracts.Any(contract =>
            contract.OwnerPersonId == personId);

        if (isUsedByPillar3aContract)
        {
            throw new InvalidOperationException(
                "The person cannot be deleted because it is used by one or more Pillar 3a contracts.");
        }

        var persons = CurrentPlan.Persons
            .Where(x => x.Id != personId)
            .ToList();

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void UpdatePlanName(string name)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("Plan name is required.");
        }

        CurrentPlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = name.Trim(),
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdatePillar3aContract(Pillar3aContract contract)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        contract.Validate();

        var pillar3aContracts = CurrentPlan.Pillar3aContracts.ToList();

        var existingIndex = pillar3aContracts.FindIndex(x => x.Id == contract.Id);

        if (existingIndex >= 0)
        {
            pillar3aContracts[existingIndex] = contract;
        }
        else
        {
            pillar3aContracts.Add(contract);
        }

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeletePillar3aContract(Guid contractId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var pillar3aContracts = CurrentPlan.Pillar3aContracts
            .Where(x => x.Id != contractId)
            .ToList();

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void AddOrUpdateHouseBuyScenario(HouseBuySimulatorScenario scenario)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var scenarios = CurrentPlan.HouseBuyScenarios.ToList();

        var existingIndex = scenarios.FindIndex(x => x.Id == scenario.Id);

        if (existingIndex >= 0)
        {
            scenarios[existingIndex] = scenario;
        }
        else
        {
            scenarios.Add(scenario);
        }

        CurrentPlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = scenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void DeleteHouseBuyScenario(Guid scenarioId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var scenarios = CurrentPlan.HouseBuyScenarios
            .Where(x => x.Id != scenarioId)
            .ToList();

        CurrentPlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,
            Persons = CurrentPlan.Persons,
            Accounts = CurrentPlan.Accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = scenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyChanged();
    }

    public void Clear()
    {
        CurrentDocument = null;
        CurrentPlan = null;
        CurrentSimulationResult = null;

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}