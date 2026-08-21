using CashFlowPlanner.Core.CreditCards;

namespace CashFlowPlanner.Core.Tests.CreditCards;

public sealed class CreditCardContractTests
{
    [Theory]
    [InlineData(15, 25)]
    [InlineData(25, 5)]
    [InlineData(31, 1)]
    [InlineData(1, 31)]
    public void Validate_Should_Accept_DistinctClosingAndPaymentDays(
        int closingDayOfMonth,
        int paymentDayOfMonth)
    {
        var contract = CreateContract(closingDayOfMonth, paymentDayOfMonth);

        contract.Validate();
    }

    [Theory]
    [InlineData(1)]
    [InlineData(15)]
    [InlineData(31)]
    public void Validate_Should_Reject_PaymentDayEqualToClosingDay(int dayOfMonth)
    {
        // A payment dated on the same day-of-month as the closing day would land
        // exactly on the next statement's closing date, making the settled amount
        // depend on event ordering within that day.
        var contract = CreateContract(dayOfMonth, dayOfMonth);

        var exception = Assert.Throws<InvalidOperationException>(contract.Validate);

        Assert.Contains("closing day", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static CreditCardContract CreateContract(
        int closingDayOfMonth,
        int paymentDayOfMonth)
    {
        return new CreditCardContract
        {
            Id = Guid.NewGuid(),
            Name = "Visa",
            CreditCardAccountId = Guid.NewGuid(),
            PaymentAccountId = Guid.NewGuid(),
            ClosingDayOfMonth = closingDayOfMonth,
            PaymentDayOfMonth = paymentDayOfMonth,
            StartDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };
    }
}
