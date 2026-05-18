using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class MortgageEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public MortgageType Type { get; set; } = MortgageType.Saron;

    public string PaymentAccountIdText { get; set; } = string.Empty;

    public string? IndirectAmortisationAccountIdText { get; set; }

    public decimal InitialPrincipal { get; set; }

    public DateOnly InitialDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public decimal? CalculationPrincipal { get; set; }

    public DateOnly? CalculationPrincipalDate { get; set; }

    public decimal FixedInterestPercent { get; set; }

    public List<MortgageInterestRatePointEditModel> SaronRates { get; set; } = [];

    public AmortisationMode AmortisationMode { get; set; } = AmortisationMode.None;

    public decimal AnnualAmortisationAmount { get; set; }

    public MortgagePaymentInterval PaymentInterval { get; set; } = MortgagePaymentInterval.Quarterly;

    public MortgageBillingCalendar BillingCalendar { get; set; } = MortgageBillingCalendar.BankQuarters;

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public static MortgageEditModel FromMortgage(MortgageContract mortgage)
    {
        return new MortgageEditModel
        {
            Id = mortgage.Id,
            Name = mortgage.Name,
            Type = mortgage.Type,
            PaymentAccountIdText = mortgage.PaymentAccountId.ToString(),
            IndirectAmortisationAccountIdText = mortgage.IndirectAmortisationAccountId?.ToString(),
            InitialPrincipal = mortgage.InitialPrincipal,
            InitialDate = mortgage.InitialDate,
            CalculationPrincipal = mortgage.CalculationPrincipal,
            CalculationPrincipalDate = mortgage.CalculationPrincipalDate,
            FixedInterestPercent = mortgage.FixedInterestPercent,
            SaronRates = mortgage.SaronRates
                .OrderBy(x => x.Date)
                .Select(MortgageInterestRatePointEditModel.FromRatePoint)
                .ToList(),
            AmortisationMode = mortgage.AmortisationMode,
            AnnualAmortisationAmount = mortgage.AnnualAmortisationAmount,
            PaymentInterval = mortgage.PaymentInterval,
            BillingCalendar = mortgage.BillingCalendar,
            EndDate = mortgage.EndDate,
            IsActive = mortgage.IsActive,
            Notes = mortgage.Notes
        };
    }

    public MortgageContract ToMortgage()
    {
        return new MortgageContract
        {
            Id = Id,
            Name = Name.Trim(),
            Type = Type,
            PaymentAccountId = ParseRequiredGuid(PaymentAccountIdText, "Payment account"),
            IndirectAmortisationAccountId = ParseOptionalGuid(IndirectAmortisationAccountIdText),
            InitialPrincipal = InitialPrincipal,
            InitialDate = InitialDate,
            CalculationPrincipal = CalculationPrincipal,
            CalculationPrincipalDate = CalculationPrincipalDate,
            FixedInterestPercent = FixedInterestPercent,
            SaronRates = Type == MortgageType.Saron
                ? SaronRates
                    .OrderBy(x => x.Date)
                    .Select(x => x.ToRatePoint())
                    .ToList()
                : [],
            AmortisationMode = AmortisationMode,
            AnnualAmortisationAmount = AnnualAmortisationAmount,
            PaymentInterval = PaymentInterval,
            BillingCalendar = BillingCalendar,
            EndDate = EndDate,
            IsActive = IsActive,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }

    private static Guid ParseRequiredGuid(string? value, string fieldName)
    {
        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            throw new InvalidOperationException($"{fieldName} is required.");
        }

        return guid;
    }

    private static Guid? ParseOptionalGuid(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!Guid.TryParse(value, out var guid) || guid == Guid.Empty)
        {
            return null;
        }

        return guid;
    }
}

public sealed class MortgageInterestRatePointEditModel
{
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public decimal RatePercent { get; set; }

    public static MortgageInterestRatePointEditModel FromRatePoint(
        MortgageInterestRatePoint ratePoint)
    {
        return new MortgageInterestRatePointEditModel
        {
            Date = ratePoint.Date,
            RatePercent = ratePoint.RatePercent
        };
    }

    public MortgageInterestRatePoint ToRatePoint()
    {
        return new MortgageInterestRatePoint
        {
            Date = Date,
            RatePercent = RatePercent
        };
    }
}