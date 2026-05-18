using CashFlowPlanner.Core;
using CashFlowPlanner.Core.CreditCards;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class CreditCardEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string CreditCardAccountIdText { get; set; } = string.Empty;

    public string PaymentAccountIdText { get; set; } = string.Empty;

    public int ClosingDayOfMonth { get; set; } = 15;

    public int PaymentDayOfMonth { get; set; } = 25;

    public CreditCardPaymentMethod PaymentMethod { get; set; } = CreditCardPaymentMethod.AutomaticLsv;

    public BusinessDayAdjustment PaymentBusinessDayAdjustment { get; set; } = BusinessDayAdjustment.NextBusinessDay;

    public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateOnly? EndDate { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public static CreditCardEditModel FromCreditCard(CreditCardContract creditCard)
    {
        return new CreditCardEditModel
        {
            Id = creditCard.Id,
            Name = creditCard.Name,
            CreditCardAccountIdText = creditCard.CreditCardAccountId.ToString(),
            PaymentAccountIdText = creditCard.PaymentAccountId.ToString(),
            ClosingDayOfMonth = creditCard.ClosingDayOfMonth,
            PaymentDayOfMonth = creditCard.PaymentDayOfMonth,
            PaymentMethod = creditCard.PaymentMethod,
            PaymentBusinessDayAdjustment = creditCard.PaymentBusinessDayAdjustment,
            StartDate = creditCard.StartDate,
            EndDate = creditCard.EndDate,
            IsActive = creditCard.IsActive,
            Notes = creditCard.Notes
        };
    }

    public CreditCardContract ToCreditCard()
    {
        return new CreditCardContract
        {
            Id = Id,
            Name = Name.Trim(),
            CreditCardAccountId = ParseRequiredGuid(CreditCardAccountIdText, "Credit card account"),
            PaymentAccountId = ParseRequiredGuid(PaymentAccountIdText, "Payment account"),
            ClosingDayOfMonth = ClosingDayOfMonth,
            PaymentDayOfMonth = PaymentDayOfMonth,
            PaymentMethod = PaymentMethod,
            PaymentBusinessDayAdjustment = PaymentBusinessDayAdjustment,
            StartDate = StartDate,
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
}