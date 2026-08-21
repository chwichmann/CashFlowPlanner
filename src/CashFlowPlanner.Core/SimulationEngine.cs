using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.Core;

public sealed class SimulationEngine
{
    private readonly CashFlowEventGenerator _eventGenerator;
    private readonly MortgageEventGenerator _mortgageEventGenerator;
    private readonly CreditCardPaymentEventGenerator _creditCardPaymentEventGenerator;
    private readonly AccountInterestEventGenerator _accountInterestEventGenerator;
    private readonly Pillar3aEventGenerator _pillar3aEventGenerator;

    public SimulationEngine()
        : this(
            new CashFlowEventGenerator(),
            new MortgageEventGenerator(),
            new CreditCardPaymentEventGenerator(),
            new AccountInterestEventGenerator(),
            new Pillar3aEventGenerator())
    {
    }

    public SimulationEngine(
        CashFlowEventGenerator eventGenerator,
        MortgageEventGenerator mortgageEventGenerator,
        CreditCardPaymentEventGenerator creditCardPaymentEventGenerator)
        : this(
            eventGenerator,
            mortgageEventGenerator,
            creditCardPaymentEventGenerator,
            new AccountInterestEventGenerator(),
            new Pillar3aEventGenerator())
    {
    }

    public SimulationEngine(
        CashFlowEventGenerator eventGenerator,
        MortgageEventGenerator mortgageEventGenerator,
        CreditCardPaymentEventGenerator creditCardPaymentEventGenerator,
        AccountInterestEventGenerator accountInterestEventGenerator)
        : this(
            eventGenerator,
            mortgageEventGenerator,
            creditCardPaymentEventGenerator,
            accountInterestEventGenerator,
            new Pillar3aEventGenerator())
    {
    }

    public SimulationEngine(
        CashFlowEventGenerator eventGenerator,
        MortgageEventGenerator mortgageEventGenerator,
        CreditCardPaymentEventGenerator creditCardPaymentEventGenerator,
        AccountInterestEventGenerator accountInterestEventGenerator,
        Pillar3aEventGenerator pillar3aEventGenerator)
    {
        _eventGenerator = eventGenerator;
        _mortgageEventGenerator = mortgageEventGenerator;
        _creditCardPaymentEventGenerator = creditCardPaymentEventGenerator;
        _accountInterestEventGenerator = accountInterestEventGenerator;
        _pillar3aEventGenerator = pillar3aEventGenerator;
    }

    public SimulationResult Simulate(CashFlowPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        plan.Validate();

        var settings = plan.SimulationSettings;
        var effectiveRange = settings.GetEffectiveDateRange(DateOnly.FromDateTime(DateTime.Today));

        var simulationStart = effectiveRange.StartDate;
        var simulationEnd = effectiveRange.EndDate;

        var accounts = plan.Accounts
            .Where(x => settings.IncludeInactiveAccounts || x.IsActive)
            .ToList();

        var accountById = accounts.ToDictionary(x => x.Id);

        var balances = accounts.ToDictionary(
            x => x.Id,
            x => x.OpeningDate <= simulationStart ? x.OpeningBalance : 0m);

        var openingBalanceApplied = accounts.ToDictionary(
            x => x.Id,
            x => x.OpeningDate <= simulationStart);

        // Important:
        // Use the plan-aware overload so transaction schedules can apply
        // plan.BankOffDays and plan.TreatWeekendsAsBankOffDays.
        var transactionEvents = _eventGenerator.GenerateEvents(
            plan,
            simulationStart,
            simulationEnd);

        var mortgageGeneration = _mortgageEventGenerator.Generate(
            plan.Mortgages,
            simulationStart,
            simulationEnd,
            plan.BaseCurrency);

        var mortgageEvents = mortgageGeneration.Events;

        var pillar3aEvents = _pillar3aEventGenerator.GenerateEvents(
            plan.Pillar3aContracts,
            simulationStart,
            simulationEnd);

        var baseEvents = transactionEvents
            .Concat(mortgageEvents)
            .Concat(pillar3aEvents)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();

        // Everything else runs against the IsActive-filtered account list; this
        // call used to be handed plan.Accounts unfiltered, so a contract on an
        // account that is not being simulated still produced payment events. The
        // engine then had no balance for the card, so every one of them raised a
        // spurious UNKNOWN_ACCOUNT critical -- and the payment leg still debited
        // the bank account, moving money out to settle a card nobody was
        // tracking.
        var creditCardsInScope = plan.CreditCards
            .Where(x => accountById.ContainsKey(x.CreditCardAccountId))
            .ToList();

        var creditCardPaymentEvents = _creditCardPaymentEventGenerator.GenerateEvents(
            creditCardsInScope,
            accounts,
            baseEvents,
            simulationStart,
            simulationEnd);

        var eventsBeforeInterest = baseEvents
            .Concat(creditCardPaymentEvents)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();

        var interestEvents = _accountInterestEventGenerator.GenerateEvents(
            accounts,
            eventsBeforeInterest,
            simulationStart,
            simulationEnd);

        var events = eventsBeforeInterest
            .Concat(interestEvents)
            .OrderBy(x => x.Date)
            .ThenBy(x => x.Priority)
            .ThenBy(x => x.Name)
            .ToList();

        var eventsByDate = events
            .GroupBy(x => x.Date)
            .ToDictionary(
                x => x.Key,
                x => x
                    .OrderBy(e => e.Priority)
                    .ThenBy(e => e.Name)
                    .ToList());

        var balancePoints = new List<AccountBalancePoint>();

        var warnings = new List<SimulationWarning>(mortgageGeneration.Warnings);

        // One warning per overdrawn account per DAY meant 7'305 warnings for a
        // single overdrawn account over 20 years -- a list nobody can read, and
        // it drowned out the criticals. Collapsed to one per contiguous episode.
        var negativeEpisodes = new Dictionary<Guid, NegativeBalanceEpisode>();

        for (var date = simulationStart; date <= simulationEnd; date = date.AddDays(1))
        {
            ApplyOpeningBalancesForDate(
                accounts,
                balances,
                openingBalanceApplied,
                date);

            if (eventsByDate.TryGetValue(date, out var dayEvents))
            {
                foreach (var cashFlowEvent in dayEvents)
                {
                    ApplyEvent(cashFlowEvent, balances, accountById, warnings);
                }
            }

            foreach (var account in accounts)
            {
                var balance = balances[account.Id];

                balancePoints.Add(new AccountBalancePoint
                {
                    Date = date,
                    AccountId = account.Id,
                    Balance = balance,
                    Currency = account.Currency
                });

                TrackNegativeBalance(
                    settings,
                    account,
                    balance,
                    date,
                    negativeEpisodes,
                    warnings);
            }
        }

        // Episodes still open on the last simulated day never got a closing day
        // to report them.
        foreach (var account in accounts)
        {
            if (negativeEpisodes.Remove(account.Id, out var episode))
            {
                warnings.Add(NegativeBalanceWarning(account, episode));
            }
        }

        return new SimulationResult
        {
            StartDate = simulationStart,
            EndDate = simulationEnd,
            Events = events,
            BalancePoints = balancePoints,
            MortgagePrincipalPoints = mortgageGeneration.PrincipalPoints,
            Warnings = warnings
        };
    }

    private static void ApplyOpeningBalancesForDate(
        IReadOnlyCollection<Account> accounts,
        Dictionary<Guid, decimal> balances,
        Dictionary<Guid, bool> openingBalanceApplied,
        DateOnly date)
    {
        foreach (var account in accounts)
        {
            if (openingBalanceApplied[account.Id])
            {
                continue;
            }

            if (account.OpeningDate != date)
            {
                continue;
            }

            balances[account.Id] += account.OpeningBalance;
            openingBalanceApplied[account.Id] = true;
        }
    }

    /// <summary>
    /// Applies one event to the running balances. The arithmetic itself lives in
    /// <see cref="AccountPosting"/> so the engine, the account statement, the
    /// interest generator and the credit-card generator cannot drift apart again.
    /// </summary>
    private static void ApplyEvent(
        CashFlowEvent cashFlowEvent,
        Dictionary<Guid, decimal> balances,
        IReadOnlyDictionary<Guid, Account> accountById,
        List<SimulationWarning> warnings)
    {
        if (!AccountPosting.TryGetLegs(cashFlowEvent, out var legs))
        {
            warnings.Add(new SimulationWarning
            {
                Code = "UNSUPPORTED_EVENT_KIND",
                Message = $"Unsupported event kind '{cashFlowEvent.Kind}' for event '{cashFlowEvent.Name}'.",
                Severity = WarningSeverity.Critical,
                Date = cashFlowEvent.Date,
                SourceId = cashFlowEvent.SourceTransactionId
            });

            return;
        }

        foreach (var leg in legs)
        {
            if (leg.AccountId is null)
            {
                warnings.Add(MissingAccountWarning(cashFlowEvent, leg.OperationName));
                continue;
            }

            if (!balances.ContainsKey(leg.AccountId.Value) ||
                !accountById.TryGetValue(leg.AccountId.Value, out var account))
            {
                warnings.Add(UnknownAccountWarning(cashFlowEvent, leg.AccountId.Value));
                continue;
            }

            // H7: the last line of defence. CashFlowPlan.Validate() already
            // rejects a cross-currency transaction or Pillar 3a schedule, but
            // contract-derived events -- mortgage, credit-card payment -- get
            // their currency from the contract, not from the account they land
            // on, so a mismatch can still reach here.
            //
            // The posting is still applied: dropping it would silently make money
            // disappear, which is harder to notice than a critical warning next
            // to a number that is visibly in the wrong currency.
            if (!Money.IsSameCurrency(cashFlowEvent.Currency, account.Currency))
            {
                warnings.Add(CurrencyMismatchWarning(cashFlowEvent, account));
            }

            balances[leg.AccountId.Value] += leg.SignedAmount;
        }
    }

    private static SimulationWarning MissingAccountWarning(
        CashFlowEvent cashFlowEvent,
        string operation)
    {
        return new SimulationWarning
        {
            Code = "MISSING_ACCOUNT",
            Message = $"Cannot {operation} account for event '{cashFlowEvent.Name}' on {cashFlowEvent.Date:yyyy-MM-dd}.",
            Severity = WarningSeverity.Critical,
            Date = cashFlowEvent.Date,
            SourceId = cashFlowEvent.SourceTransactionId
        };
    }

    private static SimulationWarning UnknownAccountWarning(
        CashFlowEvent cashFlowEvent,
        Guid accountId)
    {
        return new SimulationWarning
        {
            Code = "UNKNOWN_ACCOUNT",
            Message = $"Event '{cashFlowEvent.Name}' references unknown or inactive account '{accountId}'.",
            Severity = WarningSeverity.Critical,
            Date = cashFlowEvent.Date,
            AccountId = accountId,
            SourceId = cashFlowEvent.SourceTransactionId
        };
    }

    private static SimulationWarning CurrencyMismatchWarning(
        CashFlowEvent cashFlowEvent,
        Account account)
    {
        return new SimulationWarning
        {
            Code = "CURRENCY_MISMATCH",
            Message =
                $"Event '{cashFlowEvent.Name}' on {cashFlowEvent.Date:yyyy-MM-dd} is in " +
                $"{cashFlowEvent.Currency} but account '{account.Name}' is in {account.Currency}. " +
                "The amount was posted unconverted.",
            Severity = WarningSeverity.Critical,
            Date = cashFlowEvent.Date,
            AccountId = account.Id,
            SourceId = cashFlowEvent.SourceTransactionId
        };
    }

    private static bool IsLiquidityAccount(Account account)
    {
        return account.Type is AccountType.BankAccount
            or AccountType.SavingsAccount
            or AccountType.Cash;
    }

    /// <summary>
    /// Opens, extends or closes the account's current overdraft episode.
    /// A warning is raised once, when the episode ends.
    /// </summary>
    private static void TrackNegativeBalance(
        SimulationSettings settings,
        Account account,
        decimal balance,
        DateOnly date,
        Dictionary<Guid, NegativeBalanceEpisode> episodes,
        List<SimulationWarning> warnings)
    {
        var isOverdrawn =
            settings.WarnOnNegativeBankBalance &&
            IsLiquidityAccount(account) &&
            balance < 0m;

        if (isOverdrawn)
        {
            if (episodes.TryGetValue(account.Id, out var openEpisode))
            {
                openEpisode.LastDate = date;

                if (balance < openEpisode.MinimumBalance)
                {
                    openEpisode.MinimumBalance = balance;
                    openEpisode.MinimumDate = date;
                }

                return;
            }

            episodes[account.Id] = new NegativeBalanceEpisode
            {
                FirstDate = date,
                LastDate = date,
                MinimumBalance = balance,
                MinimumDate = date
            };

            return;
        }

        if (episodes.Remove(account.Id, out var finishedEpisode))
        {
            warnings.Add(NegativeBalanceWarning(account, finishedEpisode));
        }
    }

    private static SimulationWarning NegativeBalanceWarning(
        Account account,
        NegativeBalanceEpisode episode)
    {
        var days = episode.LastDate.DayNumber - episode.FirstDate.DayNumber + 1;

        return new SimulationWarning
        {
            Code = "NEGATIVE_BALANCE",
            Message =
                $"Account '{account.Name}' is overdrawn from {episode.FirstDate:yyyy-MM-dd} " +
                $"to {episode.LastDate:yyyy-MM-dd} ({days} day(s)), reaching " +
                $"{episode.MinimumBalance:N2} {account.Currency} on {episode.MinimumDate:yyyy-MM-dd}.",
            Severity = WarningSeverity.Warning,
            Date = episode.FirstDate,
            AccountId = account.Id
        };
    }

    private sealed class NegativeBalanceEpisode
    {
        public required DateOnly FirstDate { get; init; }

        public required DateOnly LastDate { get; set; }

        public required decimal MinimumBalance { get; set; }

        public required DateOnly MinimumDate { get; set; }
    }
}