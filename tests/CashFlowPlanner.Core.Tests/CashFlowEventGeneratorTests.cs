using CashFlowPlanner.Core;

namespace CashFlowPlanner.Core.Tests;

public sealed class CashFlowEventGeneratorTests
{
    [Fact]
    public void GenerateEvents_Should_CreateEventForOneTimeTransaction()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var transaction = TestPlanBuilder.ExternalIncome(
            toAccountId: accountId,
            amount: 5000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 25)),
            name: "Salary");

        var generator = new CashFlowEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [transaction],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Single(events);

        var cashFlowEvent = events[0];

        Assert.Equal("Salary", cashFlowEvent.Name);
        Assert.Equal(new DateOnly(2026, 6, 25), cashFlowEvent.Date);
        Assert.Equal(TransactionKind.ExternalIncome, cashFlowEvent.Kind);
        Assert.Equal(5000m, cashFlowEvent.Amount);
        Assert.Equal(accountId, cashFlowEvent.ToAccountId);
    }

    [Fact]
    public void GenerateEvents_Should_IgnoreInactiveTransactions()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var transaction = new TransactionDefinition
        {
            Name = "Inactive Salary",
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = accountId,
            Amount = 5000m,
            Currency = "CHF",
            Schedule = TestPlanBuilder.Once(new DateOnly(2026, 6, 25)),
            IsActive = false
        };

        var generator = new CashFlowEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [transaction],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void GenerateEvents_Should_OrderEventsByDatePriorityAndName()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var rent = TestPlanBuilder.ExternalExpense(
            fromAccountId: accountId,
            amount: 2000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Rent",
            priority: 100);

        var salary = TestPlanBuilder.ExternalIncome(
            toAccountId: accountId,
            amount: 5000m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 1)),
            name: "Salary",
            priority: 10);

        var generator = new CashFlowEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [rent, salary],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Equal(2, events.Count);
        Assert.Equal("Salary", events[0].Name);
        Assert.Equal("Rent", events[1].Name);
    }

    [Fact]
    public void GenerateEvents_Should_GenerateMonthlyEvents()
    {
        // Arrange
        var accountId = Guid.NewGuid();

        var transaction = TestPlanBuilder.ExternalExpense(
            fromAccountId: accountId,
            amount: 450m,
            schedule: TestPlanBuilder.Monthly(
                startDate: new DateOnly(2026, 6, 5),
                dayOfMonth: 5,
                endDate: new DateOnly(2026, 8, 5)),
            name: "Car Leasing");

        var generator = new CashFlowEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [transaction],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 12, 31));

        // Assert
        Assert.Equal(3, events.Count);

        Assert.Equal(new DateOnly(2026, 6, 5), events[0].Date);
        Assert.Equal(new DateOnly(2026, 7, 5), events[1].Date);
        Assert.Equal(new DateOnly(2026, 8, 5), events[2].Date);
    }
}