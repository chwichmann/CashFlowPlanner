namespace CashFlowPlanner.Core.CreditCards;

public sealed class CreditCardContract
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public required string Name { get; init; }

    public Guid CreditCardAccountId { get; init; }

    public Guid PaymentAccountId { get; init; }

    public int ClosingDayOfMonth { get; init; } = 15;

    public int PaymentDayOfMonth { get; init; } = 25;

    public CreditCardPaymentMethod PaymentMethod { get; init; } = CreditCardPaymentMethod.AutomaticLsv;

    public BusinessDayAdjustment PaymentBusinessDayAdjustment { get; init; } = BusinessDayAdjustment.NextBusinessDay;

    public DateOnly StartDate { get; init; }

    public DateOnly? EndDate { get; init; }

    public bool IsActive { get; init; } = true;

    public string? Notes { get; init; }

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("Credit card contract name is required.");
        }

        if (CreditCardAccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Credit card contract '{Name}' requires a credit card account.");
        }

        if (PaymentAccountId == Guid.Empty)
        {
            throw new InvalidOperationException($"Credit card contract '{Name}' requires a payment account.");
        }

        if (ClosingDayOfMonth is < 1 or > 31)
        {
            throw new InvalidOperationException($"Credit card contract '{Name}' closing day must be between 1 and 31.");
        }

        if (PaymentDayOfMonth is < 1 or > 31)
        {
            throw new InvalidOperationException($"Credit card contract '{Name}' payment day must be between 1 and 31.");
        }

        // The two day fields are not independent: the payment settles a statement
        // that has already closed. Equal days would date the payment exactly on the
        // next statement's closing date, so the settled amount would depend on the
        // ordering of events within that single day.
        if (PaymentDayOfMonth == ClosingDayOfMonth)
        {
            throw new InvalidOperationException(
                $"Credit card contract '{Name}' must not use the same day of month for the closing day and the payment day.");
        }

        if (EndDate is not null && EndDate < StartDate)
        {
            throw new InvalidOperationException($"Credit card contract '{Name}' end date must not be before start date.");
        }
    }
}