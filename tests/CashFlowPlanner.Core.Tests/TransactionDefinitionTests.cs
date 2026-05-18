namespace CashFlowPlanner.Core.Tests;

public sealed class TransactionDefinitionTests
{
    [Fact]
    public void Validate_ExternalIncomeWithoutToAccountId_Throws()
    {
        var transaction = new TransactionDefinition
        {
            Id = Guid.NewGuid(),
            Name = "Broken Salary",
            Kind = TransactionKind.ExternalIncome,
            FromAccountId = null,
            ToAccountId = null,
            Amount = 5000m,
            Currency = "CHF",
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Once,
                StartDate = new DateOnly(2026, 6, 25),
                Interval = 1,
                BusinessDayAdjustment = BusinessDayAdjustment.None
            },
            PaymentMethod = PaymentMethod.BankTransfer,
            Priority = 10,
            IsActive = true
        };

        var exception = Assert.Throws<InvalidOperationException>(
            transaction.Validate);

        Assert.Contains("requires ToAccountId", exception.Message);
    }
}