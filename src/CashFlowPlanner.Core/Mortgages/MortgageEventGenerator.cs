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

        foreach (var mortgage in mortgages.Where(x => x.IsActive))
        {
            mortgage.Validate();

            var result = GenerateForMortgage(
                mortgage,
                simulationStart,
                simulationEnd);

            events.AddRange(result.Events);
            principalPoints.AddRange(result.PrincipalPoints);
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
                .ToList()
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

        var calculationPrincipalDate = mortgage.GetCalculationPrincipalDate();
        var principal = mortgage.GetCalculationPrincipal();

        var effectiveSimulationStart = Max(simulationStart, calculationPrincipalDate);

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
            PrincipalPoints = principalPoints
        };
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