using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aEventGenerator
{
    /// <summary>
    /// Same slot as the mortgage's indirect amortisation: after salary, before
    /// discretionary spending.
    /// </summary>
    private const int Pillar3aPriority = 45;

    private readonly CashFlowEventGenerator _eventGenerator;

    public Pillar3aEventGenerator()
        : this(new CashFlowEventGenerator())
    {
    }

    public Pillar3aEventGenerator(CashFlowEventGenerator eventGenerator)
    {
        _eventGenerator = eventGenerator;
    }

    /// <summary>
    /// Turns Pillar 3a contracts into cash-flow events.
    ///
    /// Two defects are fixed here.
    ///
    /// H8 -- contributions used to be emitted as an
    /// <see cref="TransactionKind.ExternalExpense"/> with no target account, so
    /// the payment account was debited and the contract's value was credited to
    /// nothing. A household saving CHF 7'258 a year got poorer by exactly that
    /// amount every year with nothing to show for it. A contract linked to its
    /// <see cref="AccountType.Pillar3a"/> account now posts an
    /// <see cref="TransactionKind.InternalTransfer"/>, exactly as
    /// <see cref="Mortgages.MortgageEventGenerator"/> already did for indirect
    /// amortisation.
    ///
    /// Withdrawals -- <see cref="Pillar3aContract.Withdrawals"/> was validated
    /// and persisted but never read, so a retirement payout of a lifetime's
    /// savings never appeared in the cash flow at all. It does now, and a
    /// closing withdrawal also stops the contract's contributions.
    /// </summary>
    public Pillar3aGenerationResult Generate(
        IReadOnlyCollection<Pillar3aContract> contracts,
        IReadOnlyCollection<Account> accounts,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        ArgumentNullException.ThrowIfNull(contracts);
        ArgumentNullException.ThrowIfNull(accounts);

        var events = new List<CashFlowEvent>();
        var warnings = new List<SimulationWarning>();

        var accountsById = accounts.ToDictionary(x => x.Id);

        foreach (var contract in contracts.Where(x => x.IsActive))
        {
            contract.Validate();

            GenerateForContract(
                contract,
                accountsById,
                simulationStart,
                simulationEnd,
                events,
                warnings);
        }

        return new Pillar3aGenerationResult
        {
            Events = Sort(events),
            Warnings = warnings
        };
    }

    private void GenerateForContract(
        Pillar3aContract contract,
        IReadOnlyDictionary<Guid, Account> accountsById,
        DateOnly simulationStart,
        DateOnly simulationEnd,
        List<CashFlowEvent> events,
        List<SimulationWarning> warnings)
    {
        var contractAccount = ResolveContractAccount(
            contract,
            accountsById,
            simulationStart,
            warnings);

        var closingDate = contract.GetClosingDate();

        var contributionEnd = closingDate is null || closingDate.Value > simulationEnd
            ? simulationEnd
            : closingDate.Value;

        var contributionEvents = GenerateContributions(
            contract,
            contractAccount,
            simulationStart,
            contributionEnd);

        events.AddRange(contributionEvents);

        GenerateWithdrawals(
            contract,
            contractAccount,
            contributionEvents,
            simulationStart,
            simulationEnd,
            events,
            warnings);
    }

    /// <summary>
    /// The Pillar 3a account this contract pays into, or <c>null</c> when the
    /// contract names none or names one that is not being simulated.
    /// </summary>
    private static Account? ResolveContractAccount(
        Pillar3aContract contract,
        IReadOnlyDictionary<Guid, Account> accountsById,
        DateOnly simulationStart,
        List<SimulationWarning> warnings)
    {
        if (contract.AccountId is null)
        {
            if (contract.ContributionSchedules.Count > 0)
            {
                warnings.Add(new SimulationWarning
                {
                    Code = "PILLAR3A_CONTRACT_NOT_LINKED",
                    Message =
                        $"Pillar 3a contract '{contract.Name}' is not linked to a Pillar 3a account. " +
                        "Its contributions leave the plan instead of being transferred, and its " +
                        "balance is not part of net worth. Link the contract to a Pillar 3a account.",
                    Severity = WarningSeverity.Warning,
                    Date = simulationStart,
                    SourceId = contract.Id
                });
            }

            return null;
        }

        if (accountsById.TryGetValue(contract.AccountId.Value, out var account))
        {
            return account;
        }

        warnings.Add(new SimulationWarning
        {
            Code = "PILLAR3A_ACCOUNT_NOT_SIMULATED",
            Message =
                $"Pillar 3a contract '{contract.Name}' is linked to account " +
                $"'{contract.AccountId.Value}', which is not part of this simulation. " +
                "Its contributions leave the plan instead of being transferred.",
            Severity = WarningSeverity.Warning,
            Date = simulationStart,
            AccountId = contract.AccountId.Value,
            SourceId = contract.Id
        });

        return null;
    }

    private List<CashFlowEvent> GenerateContributions(
        Pillar3aContract contract,
        Account? contractAccount,
        DateOnly simulationStart,
        DateOnly simulationEnd)
    {
        var events = new List<CashFlowEvent>();

        if (simulationEnd < simulationStart)
        {
            return events;
        }

        foreach (var schedule in contract.ContributionSchedules.Where(x => x.IsActive))
        {
            schedule.Validate(contract.Name);

            var transaction = CreateContributionDefinition(
                contract,
                schedule,
                contractAccount);

            events.AddRange(_eventGenerator.GenerateEvents(
                [transaction],
                simulationStart,
                simulationEnd));
        }

        return events
            .OrderBy(x => x.Date)
            .ToList();
    }

    private static TransactionDefinition CreateContributionDefinition(
        Pillar3aContract contract,
        Pillar3aContributionSchedule schedule,
        Account? contractAccount)
    {
        var isLinked = contractAccount is not null;

        return new TransactionDefinition
        {
            // Use the contract as source, because this event is generated
            // from the Pillar 3a contract, not from a normal user transaction.
            Id = contract.Id,

            Name = $"{contract.Name} contribution",

            Kind = isLinked
                ? TransactionKind.InternalTransfer
                : TransactionKind.ExternalExpense,

            FromAccountId = schedule.PaymentAccountId,
            ToAccountId = contractAccount?.Id,

            Amount = schedule.Amount,
            Currency = schedule.Currency,

            Schedule = schedule.ToSchedule(),

            Category = "Pillar 3a Contribution",
            Counterparty = string.IsNullOrWhiteSpace(contract.ProviderName)
                ? contract.Name
                : contract.ProviderName,

            PaymentMethod = PaymentMethod.BankTransfer,

            Priority = Pillar3aPriority,

            IsActive = schedule.IsActive,

            Notes = "Generated from Pillar 3a contract."
        };
    }

    /// <summary>
    /// Emits one event per withdrawal that falls inside the simulated range.
    ///
    /// A withdrawal with an explicit amount is exact. A closing withdrawal with
    /// no amount sweeps whatever the contract holds, which this generator can
    /// only know from the linked account's opening balance plus the
    /// contributions it just generated: account interest is generated last, by
    /// <see cref="SimulationEngine"/>, and manual transfers into the account are
    /// not visible here. When either could be in play the sweep is flagged
    /// rather than quietly understated.
    /// </summary>
    private static void GenerateWithdrawals(
        Pillar3aContract contract,
        Account? contractAccount,
        IReadOnlyList<CashFlowEvent> contributionEvents,
        DateOnly simulationStart,
        DateOnly simulationEnd,
        List<CashFlowEvent> events,
        List<SimulationWarning> warnings)
    {
        var withdrawals = contract.Withdrawals
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Id)
            .ToList();

        if (withdrawals.Count == 0)
        {
            return;
        }

        var withdrawnSoFar = 0m;

        foreach (var withdrawal in withdrawals)
        {
            if (withdrawal.Date < simulationStart || withdrawal.Date > simulationEnd)
            {
                continue;
            }

            var amount = withdrawal.Amount ?? ResolveClosingAmount(
                contract,
                contractAccount,
                contributionEvents,
                withdrawal,
                withdrawnSoFar,
                warnings);

            if (amount <= 0m)
            {
                if (withdrawal.CloseContract)
                {
                    break;
                }

                continue;
            }

            withdrawnSoFar += amount;

            var cashFlowEvent = CreateWithdrawalEvent(
                contract,
                contractAccount,
                withdrawal,
                amount);

            if (cashFlowEvent is null)
            {
                warnings.Add(new SimulationWarning
                {
                    Code = "PILLAR3A_WITHDRAWAL_NOT_POSTED",
                    Message =
                        $"Pillar 3a withdrawal from '{contract.Name}' on {withdrawal.Date:yyyy-MM-dd} " +
                        "names neither a Pillar 3a account to take the money from nor a target " +
                        "account to pay it into, so it could not be posted.",
                    Severity = WarningSeverity.Critical,
                    Date = withdrawal.Date,
                    SourceId = contract.Id
                });
            }
            else
            {
                events.Add(cashFlowEvent);
            }

            if (withdrawal.CloseContract)
            {
                break;
            }
        }
    }

    private static decimal ResolveClosingAmount(
        Pillar3aContract contract,
        Account? contractAccount,
        IReadOnlyList<CashFlowEvent> contributionEvents,
        Pillar3aWithdrawalEvent withdrawal,
        decimal withdrawnSoFar,
        List<SimulationWarning> warnings)
    {
        var opening = contractAccount is null
            ? contract.OpeningValue
            : contractAccount.OpeningDate <= withdrawal.Date
                ? contractAccount.OpeningBalance
                : 0m;

        var contributed = contributionEvents
            .Where(x => x.Date <= withdrawal.Date)
            .Sum(x => x.Amount);

        if (contractAccount is not null &&
            contractAccount.InterestContracts.Count > 0)
        {
            warnings.Add(new SimulationWarning
            {
                Code = "PILLAR3A_CLOSE_IGNORES_GROWTH",
                Message =
                    $"Pillar 3a contract '{contract.Name}' closes on {withdrawal.Date:yyyy-MM-dd} " +
                    "without a stated amount. The payout was computed from the opening balance and " +
                    "the contributions only; interest credited to account " +
                    $"'{contractAccount.Name}' is not included and stays behind on that account. " +
                    "State the withdrawal amount explicitly to remove this approximation.",
                Severity = WarningSeverity.Warning,
                Date = withdrawal.Date,
                AccountId = contractAccount.Id,
                SourceId = contract.Id
            });
        }

        var balance = opening + contributed - withdrawnSoFar;

        return balance < 0m ? 0m : balance;
    }

    /// <summary>
    /// The posting shape of a withdrawal, which depends on what the contract
    /// knows about:
    /// account and target  -> transfer between them;
    /// account only        -> the money leaves the plan (a WEF withdrawal used
    ///                        directly for a property purchase, say);
    /// target only         -> untracked contract paying into a tracked account;
    /// neither             -> nothing to post.
    /// </summary>
    private static CashFlowEvent? CreateWithdrawalEvent(
        Pillar3aContract contract,
        Account? contractAccount,
        Pillar3aWithdrawalEvent withdrawal,
        decimal amount)
    {
        var kind = (contractAccount, withdrawal.TargetAccountId) switch
        {
            (not null, not null) => TransactionKind.InternalTransfer,
            (not null, null) => TransactionKind.ExternalExpense,
            (null, not null) => TransactionKind.ExternalIncome,
            _ => (TransactionKind?)null
        };

        if (kind is null)
        {
            return null;
        }

        return new CashFlowEvent
        {
            SourceTransactionId = contract.Id,
            Name = $"{contract.Name} withdrawal",
            Date = withdrawal.Date,
            Kind = kind.Value,
            FromAccountId = contractAccount?.Id,
            ToAccountId = withdrawal.TargetAccountId,
            Amount = amount,
            Currency = contract.Currency,
            Priority = Pillar3aPriority,
            Category = "Pillar 3a Withdrawal",
            Counterparty = string.IsNullOrWhiteSpace(contract.ProviderName)
                ? contract.Name
                : contract.ProviderName,
            PaymentMethod = PaymentMethod.BankTransfer,
            Notes = string.IsNullOrWhiteSpace(withdrawal.Notes)
                ? $"Generated from Pillar 3a contract ({withdrawal.Reason})."
                : withdrawal.Notes
        };
    }

    private static IReadOnlyList<CashFlowEvent> Sort(IEnumerable<CashFlowEvent> events)
    {
        return events
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();
    }
}
