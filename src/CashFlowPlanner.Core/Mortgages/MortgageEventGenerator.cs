namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageEventGenerator
{
    private readonly MortgageBillingPeriodGenerator _billingPeriodGenerator;

    public MortgageEventGenerator()
        : this(new MortgageBillingPeriodGenerator())
    {
    }

    public MortgageEventGenerator(MortgageBillingPeriodGenerator billingPeriodGenerator)
    {
        _billingPeriodGenerator = billingPeriodGenerator;
    }

    public MortgageGenerationResult Generate(
    IEnumerable<MortgageContract> mortgages,
    DateOnly simulationStart,
    DateOnly simulationEnd)
    {
        var events = new List<CashFlowEvent>();
        var principalPoints = new List<MortgagePrincipalPoint>();
        var warnings = new List<SimulationWarning>();

        foreach (var mortgage in mortgages.Where(x => x.IsActive))
        {
            mortgage.Validate();

            var result = GenerateForMortgage(
                mortgage,
                simulationStart,
                simulationEnd);

            events.AddRange(result.Events);
            principalPoints.AddRange(result.PrincipalPoints);
            warnings.AddRange(result.Warnings);
        }

        return new MortgageGenerationResult
        {
            Events = events
                .OrderBy(x => x.Date)
                .ThenBy(x => x.Priority)
                .ThenBy(x => x.Name)
                .ToList(),

            PrincipalPoints = principalPoints
                .OrderBy(x => x.Date)
                .ThenBy(x => x.MortgageName)
                .ToList(),

            Warnings = warnings
        };
    }

    private MortgageGenerationResult GenerateForMortgage(
    MortgageContract mortgage,
    DateOnly simulationStart,
    DateOnly simulationEnd)
    {
        if (mortgage.PaymentInterval != MortgagePaymentInterval.Quarterly)
        {
            throw new NotSupportedException(
                $"Mortgage '{mortgage.Name}' currently supports only quarterly payment interval.");
        }

        if (mortgage.BillingCalendar != MortgageBillingCalendar.BankQuarters)
        {
            throw new NotSupportedException(
                $"Mortgage '{mortgage.Name}' currently supports only bank-quarter billing calendar.");
        }

        var events = new List<CashFlowEvent>();
        var principalPoints = new List<MortgagePrincipalPoint>();
        var warnings = new List<SimulationWarning>();

        var calculationPrincipalDate = mortgage.GetCalculationPrincipalDate();

        // H1/H2: CalculationPrincipalDate is the date the principal is KNOWN on,
        // not the date the mortgage starts existing. Anchoring the whole
        // generation on it -- Max(simulationStart, calculationPrincipalDate) --
        // was wrong in both directions: a date in the future left the mortgage
        // with no principal point and no billing periods before it, so it read as
        // zero debt and its interest was never charged, and a date in the past
        // was taken verbatim at the simulation start, ignoring every instalment
        // paid in between.
        //
        // Anchor on the first date the mortgage can exist inside the range
        // instead, and roll the known principal along the billing calendar to
        // get there.
        var effectiveSimulationStart = Max(simulationStart, mortgage.InitialDate);

        var principal = RollPrincipal(
            mortgage,
            mortgage.GetCalculationPrincipal(),
            calculationPrincipalDate,
            effectiveSimulationStart,
            out var rolledInstalments);

        if (calculationPrincipalDate > effectiveSimulationStart)
        {
            warnings.Add(new SimulationWarning
            {
                Code = "MORTGAGE_PRINCIPAL_DATE_IN_FUTURE",
                Message =
                    $"Mortgage '{mortgage.Name}' states its principal as of {calculationPrincipalDate:yyyy-MM-dd}, " +
                    $"which is after the simulated start {effectiveSimulationStart:yyyy-MM-dd}. " +
                    $"The principal before that date was extrapolated backwards over {rolledInstalments} amortisation instalment(s).",
                Severity = WarningSeverity.Warning,
                Date = effectiveSimulationStart,
                SourceId = mortgage.Id
            });
        }
        else if (calculationPrincipalDate < effectiveSimulationStart && rolledInstalments > 0)
        {
            warnings.Add(new SimulationWarning
            {
                Code = "MORTGAGE_PRINCIPAL_ROLLED_FORWARD",
                Message =
                    $"Mortgage '{mortgage.Name}' states its principal as of {calculationPrincipalDate:yyyy-MM-dd}. " +
                    $"{rolledInstalments} amortisation instalment(s) fall between that date and the simulated start " +
                    $"{effectiveSimulationStart:yyyy-MM-dd}; the principal was amortised forward to {principal:N2}. " +
                    "Enter a more recent known principal to remove this assumption.",
                Severity = WarningSeverity.Warning,
                Date = effectiveSimulationStart,
                SourceId = mortgage.Id
            });
        }

        principalPoints.Add(new MortgagePrincipalPoint
        {
            Date = effectiveSimulationStart,
            MortgageId = mortgage.Id,
            MortgageName = mortgage.Name,
            Principal = principal,
            Currency = "CHF"
        });

        var periods = _billingPeriodGenerator.GenerateBankQuarterPeriods(
            effectiveSimulationStart,
            simulationEnd);

        if (mortgage.EndDate is not null)
        {
            periods = periods
                .Where(x => x.PeriodStart <= mortgage.EndDate.Value)
                .ToList();
        }

        foreach (var period in periods)
        {
            var effectivePeriodStart = Max(period.PeriodStart, mortgage.InitialDate);

            var effectivePeriodEndExclusive = mortgage.EndDate is null
                ? period.PeriodEndExclusive
                : Min(period.PeriodEndExclusive, mortgage.EndDate.Value.AddDays(1));

            if (effectivePeriodEndExclusive <= effectivePeriodStart)
            {
                continue;
            }

            var interestAmount = CalculateInterestForPeriod(
                mortgage,
                principal,
                effectivePeriodStart,
                effectivePeriodEndExclusive);

            if (interestAmount > 0)
            {
                events.Add(CreateInterestEvent(
                    mortgage,
                    period.PaymentDate,
                    interestAmount));
            }

            var amortisationAmount = CalculatePeriodAmortisation(
                mortgage,
                principal);

            if (amortisationAmount > 0)
            {
                switch (mortgage.AmortisationMode)
                {
                    case AmortisationMode.Direct:
                        events.Add(CreateDirectAmortisationEvent(
                            mortgage,
                            period.PaymentDate,
                            amortisationAmount));

                        principal -= amortisationAmount;

                        if (principal < 0)
                        {
                            principal = 0;
                        }

                        principalPoints.Add(new MortgagePrincipalPoint
                        {
                            Date = period.PaymentDate,
                            MortgageId = mortgage.Id,
                            MortgageName = mortgage.Name,
                            Principal = principal,
                            Currency = "CHF"
                        });

                        break;

                    case AmortisationMode.Indirect:
                        events.Add(CreateIndirectAmortisationEvent(
                            mortgage,
                            period.PaymentDate,
                            amortisationAmount));

                        principalPoints.Add(new MortgagePrincipalPoint
                        {
                            Date = period.PaymentDate,
                            MortgageId = mortgage.Id,
                            MortgageName = mortgage.Name,
                            Principal = principal,
                            Currency = "CHF"
                        });

                        break;

                    case AmortisationMode.None:
                        principalPoints.Add(new MortgagePrincipalPoint
                        {
                            Date = period.PaymentDate,
                            MortgageId = mortgage.Id,
                            MortgageName = mortgage.Name,
                            Principal = principal,
                            Currency = "CHF"
                        });

                        break;

                    default:
                        throw new InvalidOperationException(
                            $"Unsupported amortisation mode '{mortgage.AmortisationMode}'.");
                }
            }
            else
            {
                principalPoints.Add(new MortgagePrincipalPoint
                {
                    Date = period.PaymentDate,
                    MortgageId = mortgage.Id,
                    MortgageName = mortgage.Name,
                    Principal = principal,
                    Currency = "CHF"
                });
            }
        }

        return new MortgageGenerationResult
        {
            Events = events,
            PrincipalPoints = principalPoints,
            Warnings = warnings
        };
    }

    /// <summary>
    /// Moves a known principal along the billing calendar from the date it was
    /// known on to <paramref name="targetDate"/>.
    ///
    /// Forward (the known date is in the past) replays the instalments that were
    /// already paid, exactly the way the generation loop below pays them.
    /// Backward (the known date is in the future) undoes the instalments that
    /// are still to be paid before it, so the figure the user typed is still the
    /// principal in force on the date they typed it against.
    /// </summary>
    private decimal RollPrincipal(
        MortgageContract mortgage,
        decimal knownPrincipal,
        DateOnly knownAtDate,
        DateOnly targetDate,
        out int rolledInstalments)
    {
        rolledInstalments = 0;

        if (knownAtDate == targetDate)
        {
            return knownPrincipal;
        }

        // Only direct amortisation moves the principal. Indirect pays into a
        // separate account and None does not amortise at all, so for both the
        // principal is the same on every date and there is nothing to roll.
        if (mortgage.AmortisationMode != AmortisationMode.Direct ||
            mortgage.AnnualAmortisationAmount <= 0)
        {
            return knownPrincipal;
        }

        var periodsPerYear = 12m / (int)mortgage.PaymentInterval;
        var instalment = mortgage.AnnualAmortisationAmount / periodsPerYear;

        var fromInclusive = Min(knownAtDate, targetDate);
        var toExclusive = Max(knownAtDate, targetDate);

        var billedPaymentDates = _billingPeriodGenerator
            .GenerateBankQuarterPeriods(fromInclusive, toExclusive)
            .Where(x => x.PaymentDate < toExclusive)
            .Where(x => IsBilledPeriod(mortgage, x))
            .ToList();

        rolledInstalments = billedPaymentDates.Count;

        if (rolledInstalments == 0)
        {
            return knownPrincipal;
        }

        if (targetDate > knownAtDate)
        {
            var principal = knownPrincipal;

            foreach (var _ in billedPaymentDates)
            {
                principal -= Math.Min(instalment, principal);

                if (principal < 0)
                {
                    principal = 0;
                }
            }

            return principal;
        }

        // Rolling backwards only ever increases the principal, so the
        // Math.Min clamp the forward direction needs cannot bite here.
        return knownPrincipal + (instalment * rolledInstalments);
    }

    /// <summary>
    /// Whether the generation loop below would actually bill
    /// <paramref name="period"/> for this mortgage -- same two guards, so the
    /// roll counts exactly the instalments the loop pays.
    /// </summary>
    private static bool IsBilledPeriod(
        MortgageContract mortgage,
        MortgageBillingPeriod period)
    {
        if (mortgage.EndDate is not null && period.PeriodStart > mortgage.EndDate.Value)
        {
            return false;
        }

        var effectivePeriodStart = Max(period.PeriodStart, mortgage.InitialDate);

        var effectivePeriodEndExclusive = mortgage.EndDate is null
            ? period.PeriodEndExclusive
            : Min(period.PeriodEndExclusive, mortgage.EndDate.Value.AddDays(1));

        return effectivePeriodEndExclusive > effectivePeriodStart;
    }

    public static decimal CalculateInterestForPeriod(
        MortgageContract mortgage,
        decimal principal,
        DateOnly periodStart,
        DateOnly periodEndExclusive)
    {
        if (periodEndExclusive <= periodStart)
        {
            return 0m;
        }

        if (principal <= 0)
        {
            return 0m;
        }

        var interest = 0m;
        var saronCurve = new MortgageRateCurve(mortgage.SaronRates);

        for (var date = periodStart; date < periodEndExclusive; date = date.AddDays(1))
        {
            var effectiveRatePercent = GetEffectiveAnnualRatePercent(
                mortgage,
                saronCurve,
                date);

            interest += principal * (effectiveRatePercent / 100m) / 365m;
        }

        return Math.Round(interest, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal GetEffectiveAnnualRatePercent(
        MortgageContract mortgage,
        MortgageRateCurve saronCurve,
        DateOnly date)
    {
        return mortgage.Type switch
        {
            MortgageType.Fixed =>
                mortgage.FixedInterestPercent,

            MortgageType.Saron =>
                mortgage.FixedInterestPercent + Math.Max(0m, saronCurve.GetRatePercent(date)),

            _ => throw new InvalidOperationException(
                $"Unsupported mortgage type '{mortgage.Type}'.")
        };
    }

    private static decimal CalculatePeriodAmortisation(
        MortgageContract mortgage,
        decimal principal)
    {
        if (mortgage.AmortisationMode == AmortisationMode.None)
        {
            return 0m;
        }

        if (mortgage.AnnualAmortisationAmount <= 0)
        {
            return 0m;
        }

        var periodsPerYear = 12m / (int)mortgage.PaymentInterval;
        var amount = mortgage.AnnualAmortisationAmount / periodsPerYear;

        return Math.Min(amount, principal);
    }

    private static CashFlowEvent CreateInterestEvent(
        MortgageContract mortgage,
        DateOnly paymentDate,
        decimal amount)
    {
        return new CashFlowEvent
        {
            SourceTransactionId = mortgage.Id,
            Name = $"{mortgage.Name} interest",
            Date = paymentDate,
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = mortgage.PaymentAccountId,
            ToAccountId = null,
            Amount = amount,
            Currency = "CHF",
            Priority = 80,
            Category = "Mortgage Interest",
            Counterparty = "Bank",
            PaymentMethod = PaymentMethod.Lsv,
            Notes = "Generated from mortgage contract."
        };
    }

    private static CashFlowEvent CreateDirectAmortisationEvent(
    MortgageContract mortgage,
    DateOnly paymentDate,
    decimal amount)
    {
        return new CashFlowEvent
        {
            SourceTransactionId = mortgage.Id,
            Name = $"{mortgage.Name} amortisation",
            Date = paymentDate,
            Kind = TransactionKind.ExternalExpense,
            FromAccountId = mortgage.PaymentAccountId,
            ToAccountId = null,
            Amount = amount,
            Currency = "CHF",
            Priority = 81,
            Category = "Mortgage Amortisation",
            Counterparty = "Bank",
            PaymentMethod = PaymentMethod.Lsv,
            Notes = "Generated from mortgage contract. Principal is tracked internally."
        };
    }

    private static CashFlowEvent CreateIndirectAmortisationEvent(
        MortgageContract mortgage,
        DateOnly paymentDate,
        decimal amount)
    {
        if (mortgage.IndirectAmortisationAccountId is null)
        {
            throw new InvalidOperationException(
                $"Mortgage '{mortgage.Name}' requires an indirect amortisation account.");
        }

        return new CashFlowEvent
        {
            SourceTransactionId = mortgage.Id,
            Name = $"{mortgage.Name} indirect amortisation",
            Date = paymentDate,
            Kind = TransactionKind.InternalTransfer,
            FromAccountId = mortgage.PaymentAccountId,
            ToAccountId = mortgage.IndirectAmortisationAccountId,
            Amount = amount,
            Currency = "CHF",
            Priority = 81,
            Category = "Mortgage Indirect Amortisation",
            Counterparty = "Pillar 3a",
            PaymentMethod = PaymentMethod.Lsv,
            Notes = "Generated from mortgage contract."
        };
    }

    private static DateOnly Max(DateOnly left, DateOnly right)
        => left >= right ? left : right;

    private static DateOnly Min(DateOnly left, DateOnly right)
        => left <= right ? left : right;
}