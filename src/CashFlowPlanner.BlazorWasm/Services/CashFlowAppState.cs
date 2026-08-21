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

    /// <summary>
    /// Raised whenever anything the UI renders changed - plan data or simulation results.
    /// Components re-render on this.
    /// </summary>
    public event Action? Changed;

    /// <summary>
    /// Raised only when the plan itself changed, so it needs to be written back.
    /// Persistence listens to this and not to <see cref="Changed"/>, because running a simulation
    /// leaves the plan byte-for-byte identical and used to force a redundant full re-serialize and
    /// localStorage write on every run (finding P3b).
    /// </summary>
    public event Action? PlanChanged;

    /// <summary>
    /// Raised when only the simulation result changed.
    /// </summary>
    public event Action? SimulationChanged;

    /// <summary>
    /// Raised whenever <see cref="IsDirty"/> flips.
    /// </summary>
    public event Action? DirtyStateChanged;

    /// <summary>
    /// True when the plan holds edits that have not been exported to a file.
    ///
    /// The plan file is the source of truth and localStorage is only a working copy, so "saved"
    /// means "exported", not "cached". Before finding P1c there was no dirty tracking at all and
    /// closing the tab discarded work in silence.
    /// </summary>
    public bool IsDirty { get; private set; }

    /// <summary>
    /// When the plan was last exported to a file, or <see langword="null"/> if it never was in
    /// this session.
    /// </summary>
    public DateTimeOffset? LastExportedAt { get; private set; }

    // Hash of the JSON the user last exported. CashFlowPlanDocumentMapper assigns collections by
    // reference, so CurrentDocument shares its lists with CurrentPlan and is a snapshot of
    // nothing - comparing against it would always report "clean". A content hash is the only
    // reliable baseline.
    private string? _exportedContentHash;

    public void LoadDocument(CashFlowPlanDocument document)
    {
        CurrentDocument = document;
        CurrentPlan = document.ToPlan();
        CurrentSimulationResult = null;

        // A freshly loaded plan matches whatever it was loaded from; edits start from here.
        _exportedContentHash = null;
        LastExportedAt = null;

        NotifyPlanChanged(markDirty: false);
    }

    /// <summary>
    /// Records that the plan was exported to a file. The hash of the exported JSON becomes the
    /// clean baseline, so undoing back to exactly that content reports clean again.
    /// </summary>
    public void MarkExported(string exportedJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(exportedJson);

        _exportedContentHash = ComputeContentHash(exportedJson);
        LastExportedAt = DateTimeOffset.UtcNow;

        SetDirty(false);
    }

    /// <summary>
    /// Called with the JSON that was just written to the browser working copy. Serializing the
    /// plan is the expensive part of a save, so the dirty flag is re-evaluated here, where the
    /// JSON already exists, rather than on every mutation.
    /// </summary>
    public void NotifyPersistedContent(string json)
    {
        if (_exportedContentHash is null || string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        SetDirty(!string.Equals(
            ComputeContentHash(json),
            _exportedContentHash,
            StringComparison.Ordinal));
    }

    private void SetDirty(bool isDirty)
    {
        if (IsDirty == isDirty)
        {
            return;
        }

        IsDirty = isDirty;

        DirtyStateChanged?.Invoke();
    }

    private static string ComputeContentHash(string json)
    {
        return Convert.ToHexString(
            System.Security.Cryptography.SHA256.HashData(
                System.Text.Encoding.UTF8.GetBytes(json)));
    }

    public void SetPlan(CashFlowPlan plan)
    {
        CurrentPlan = plan;
        CurrentDocument = plan.ToDocument(CurrentDocument);
        CurrentSimulationResult = null;

        NotifyPlanChanged();
    }

    public SimulationResult RunSimulation()
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var engine = new SimulationEngine();
        CurrentSimulationResult = engine.Simulate(CurrentPlan);

        // Deliberately not NotifyPlanChanged: the plan is unchanged, so there is nothing to save.
        NotifySimulationChanged();

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

        NotifyPlanChanged();
    }

    public void DeleteAccount(Guid accountId)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var account = CurrentPlan.Accounts.SingleOrDefault(x => x.Id == accountId);

        if (account is null)
        {
            return;
        }

        // Finding P1a: this delete used to check transactions only. Deleting an account that a
        // mortgage, a credit card or a Pillar 3a schedule still points at left a plan that neither
        // validates nor serializes, so every later autosave and every export threw and the session
        // could not be recovered.
        var usages = DescribeAccountUsages(CurrentPlan, accountId);

        if (usages.Count > 0)
        {
            throw new InvalidOperationException(
                $"The account '{account.Name}' cannot be deleted because it is still used by " +
                $"{FormatUsages(usages)}. Remove or repoint those first.");
        }

        var accounts = CurrentPlan.Accounts
            .Where(x => x.Id != accountId)
            .ToList();

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,

            DefaultPaymentAccountId =
                CurrentPlan.DefaultPaymentAccountId == accountId
                    ? null
                    : CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

            Persons = CurrentPlan.Persons,
            Accounts = accounts,
            Transactions = CurrentPlan.Transactions,
            Mortgages = CurrentPlan.Mortgages,
            CreditCards = CurrentPlan.CreditCards,
            Pillar3aContracts = CurrentPlan.Pillar3aContracts,
            HouseBuyScenarios = CurrentPlan.HouseBuyScenarios,
            SimulationSettings = CurrentPlan.SimulationSettings
        };

        // The last line of defence: never commit a plan that cannot be saved, exactly like every
        // other delete on this type does.
        candidatePlan.Validate();

        CurrentPlan = candidatePlan;
        CurrentSimulationResult = null;
        CurrentDocument = CurrentPlan.ToDocument(CurrentDocument);

        NotifyPlanChanged();
    }

    /// <summary>
    /// Every reference to <paramref name="accountId"/> that would survive the account's deletion,
    /// phrased for the user. Plan validation does not cover mortgage and credit-card account
    /// references, so these checks are the only thing standing between the user and a plan that
    /// cannot be written back.
    /// </summary>
    public static IReadOnlyList<string> DescribeAccountUsages(CashFlowPlan plan, Guid accountId)
    {
        ArgumentNullException.ThrowIfNull(plan);

        var usages = new List<string>();

        var transactionCount = plan.Transactions.Count(x =>
            x.FromAccountId == accountId ||
            x.ToAccountId == accountId);

        if (transactionCount > 0)
        {
            usages.Add(transactionCount == 1
                ? "1 transaction"
                : $"{transactionCount} transactions");
        }

        foreach (var mortgage in plan.Mortgages)
        {
            if (mortgage.PaymentAccountId == accountId)
            {
                usages.Add($"the mortgage '{mortgage.Name}' (payment account)");
            }

            if (mortgage.IndirectAmortisationAccountId == accountId)
            {
                usages.Add($"the mortgage '{mortgage.Name}' (indirect amortisation account)");
            }
        }

        foreach (var creditCard in plan.CreditCards)
        {
            if (creditCard.CreditCardAccountId == accountId)
            {
                usages.Add($"the credit card '{creditCard.Name}' (card account)");
            }

            if (creditCard.PaymentAccountId == accountId)
            {
                usages.Add($"the credit card '{creditCard.Name}' (payment account)");
            }
        }

        foreach (var contract in plan.Pillar3aContracts)
        {
            if (contract.ContributionSchedules.Any(x => x.PaymentAccountId == accountId))
            {
                usages.Add(
                    $"the Pillar 3a contract '{contract.Name}' (contribution payment account)");
            }

            if (contract.Withdrawals.Any(x => x.TargetAccountId == accountId))
            {
                usages.Add(
                    $"the Pillar 3a contract '{contract.Name}' (withdrawal target account)");
            }
        }

        return usages;
    }

    private static string FormatUsages(IReadOnlyList<string> usages)
    {
        return usages.Count == 1
            ? usages[0]
            : string.Join("; ", usages);
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

        NotifyPlanChanged();
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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
    }

    public void UpdatePlanDefaultsAndBankCalendar(
        Guid? defaultPaymentAccountId,
        bool treatWeekendsAsBankOffDays,
        List<BankOffDay> bankOffDays)
    {
        if (CurrentPlan is null)
        {
            throw new InvalidOperationException("No cashflow plan is loaded.");
        }

        var candidatePlan = new CashFlowPlan
        {
            Id = CurrentPlan.Id,
            Name = CurrentPlan.Name,
            BaseCurrency = CurrentPlan.BaseCurrency,

            DefaultPaymentAccountId = defaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = treatWeekendsAsBankOffDays,
            BankOffDays = bankOffDays,

            Persons = CurrentPlan.Persons,
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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
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

            DefaultPaymentAccountId = CurrentPlan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = CurrentPlan.TreatWeekendsAsBankOffDays,
            BankOffDays = CurrentPlan.BankOffDays,

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

        NotifyPlanChanged();
    }

    public void Clear()
    {
        CurrentDocument = null;
        CurrentPlan = null;
        CurrentSimulationResult = null;

        _exportedContentHash = null;
        LastExportedAt = null;

        NotifyPlanChanged(markDirty: false);
    }

    /// <summary>
    /// The plan changed and has to be written back. Also raises <see cref="Changed"/> so that the
    /// UI still re-renders on a single subscription.
    /// </summary>
    private void NotifyPlanChanged(bool markDirty = true)
    {
        // Pessimistic and cheap: the plan moved, so assume it no longer matches the exported file.
        // NotifyPersistedContent corrects this the moment the JSON actually exists.
        SetDirty(markDirty && CurrentPlan is not null);

        PlanChanged?.Invoke();
        Changed?.Invoke();
    }

    /// <summary>
    /// Only the simulation result changed. The UI re-renders, persistence does not run.
    /// </summary>
    private void NotifySimulationChanged()
    {
        SimulationChanged?.Invoke();
        Changed?.Invoke();
    }
}