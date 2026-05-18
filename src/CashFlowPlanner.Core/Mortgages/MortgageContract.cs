namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgageContract
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public MortgageType Type { get; init; } = MortgageType.Fixed;

    /// <summary>
    /// Account from which the bank collects interest and amortisation.
    /// In your case this can be the savings account.
    /// </summary>
    public Guid PaymentAccountId { get; init; }

    /// <summary>
    /// Required only for indirect amortisation.
    /// Example: Pillar 3a account.
    /// </summary>
    public Guid? IndirectAmortisationAccountId { get; init; }

    /// <summary>
    /// Original mortgage amount at mortgage start.
    /// Example: 750000 CHF.
    /// </summary>
    public decimal InitialPrincipal { get; init; }

    public DateOnly InitialDate { get; init; }

    /// <summary>
    /// Optional known current principal used as simulation/calculation baseline.
    /// Example: mortgage started in 2021 with 750000,
    /// but actual known amount on 2026-01-01 is 705000.
    /// </summary>
    public decimal? CalculationPrincipal { get; init; }

    public DateOnly? CalculationPrincipalDate { get; init; }

    /// <summary>
    /// For fixed mortgages: fixed annual interest rate.
    /// For SARON mortgages: fixed margin/base interest.
    /// Percent value. Example: 0.65 means 0.65%.
    /// </summary>
    public decimal FixedInterestPercent { get; init; }

    /// <summary>
    /// Manually entered actual/expected SARON compound values.
    /// Only relevant for SARON mortgages.
    /// Interpolation is handled by MortgageRateCurve.
    /// </summary>
    public List<MortgageInterestRatePoint> SaronRates { get; init; } = [];

    public AmortisationMode AmortisationMode { get; init; } = AmortisationMode.None;

    /// <summary>
    /// Annual amortisation amount.
    /// Example: 9000 CHF/year.
    /// For quarterly direct amortisation this results in 2250 CHF per quarter.
    /// </summary>
    public decimal AnnualAmortisationAmount { get; init; }

    public MortgagePaymentInterval PaymentInterval { get; init; } = MortgagePaymentInterval.Quarterly;

    public MortgageBillingCalendar BillingCalendar { get; init; } = MortgageBillingCalendar.BankQuarters;

    public DateOnly? EndDate { get; init; }

    public bool IsActive { get; init; } = true;

    public string? Notes { get; init; }

    public decimal GetCalculationPrincipal()
        => CalculationPrincipal ?? InitialPrincipal;

    public DateOnly GetCalculationPrincipalDate()
        => CalculationPrincipalDate ?? InitialDate;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Mortgage name is required.");
        }

        if (PaymentAccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' requires a payment account.");
        }

        if (InitialPrincipal <= 0)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' requires an initial principal greater than zero.");
        }

        if (InitialDate == default)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' requires an initial date.");
        }

        if (CalculationPrincipal is <= 0)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' calculation principal must be greater than zero.");
        }

        if (CalculationPrincipalDate is not null && CalculationPrincipal is null)
        {
            throw new InvalidOperationException(
                $"Mortgage '{Name}' has a calculation principal date but no calculation principal.");
        }

        if (CalculationPrincipal is not null && CalculationPrincipalDate is null)
        {
            throw new InvalidOperationException(
                $"Mortgage '{Name}' has a calculation principal but no calculation principal date.");
        }

        if (CalculationPrincipalDate is not null && CalculationPrincipalDate < InitialDate)
        {
            throw new InvalidOperationException(
                $"Mortgage '{Name}' calculation principal date must not be before the initial date.");
        }

        if (FixedInterestPercent < 0)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' fixed interest must not be negative.");
        }

        if (AnnualAmortisationAmount < 0)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' annual amortisation must not be negative.");
        }

        if (AmortisationMode == AmortisationMode.Indirect &&
            IndirectAmortisationAccountId is null)
        {
            throw new InvalidOperationException(
                $"Mortgage '{Name}' uses indirect amortisation and requires an indirect amortisation account.");
        }

        if (EndDate is not null && EndDate < InitialDate)
        {
            throw new InvalidOperationException($"Mortgage '{Name}' end date must be after initial date.");
        }

        if (Type == MortgageType.Saron && SaronRates.Count == 0)
        {
            throw new InvalidOperationException(
                $"SARON mortgage '{Name}' requires at least one SARON rate point.");
        }

        if (PaymentInterval != MortgagePaymentInterval.Quarterly)
        {
            throw new NotSupportedException(
                $"Mortgage '{Name}' currently supports only quarterly payment interval.");
        }

        if (BillingCalendar != MortgageBillingCalendar.BankQuarters)
        {
            throw new NotSupportedException(
                $"Mortgage '{Name}' currently supports only bank-quarter billing calendar.");
        }
    }
}