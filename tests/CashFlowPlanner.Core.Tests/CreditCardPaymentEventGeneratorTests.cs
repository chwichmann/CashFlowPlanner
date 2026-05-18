using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;

namespace CashFlowPlanner.Core.Tests;

public sealed class CreditCardPaymentEventGeneratorTests
{
    [Fact]
    public void GenerateEvents_Should_CreatePayment_ForOpeningCreditCardDebt()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: -1200m);

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id);

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Single(events);

        var payment = events[0];

        Assert.Equal(new DateOnly(2026, 6, 25), payment.Date);
        Assert.Equal(TransactionKind.DebtPayment, payment.Kind);
        Assert.Equal(mainAccount.Id, payment.FromAccountId);
        Assert.Equal(creditCardAccount.Id, payment.ToAccountId);
        Assert.Equal(1200m, payment.Amount);
        Assert.Equal(PaymentMethod.Lsv, payment.PaymentMethod);
    }

    [Fact]
    public void GenerateEvents_Should_IncludePurchasesUntilClosingDay()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: 0m);

        var purchaseBeforeClosing = new CashFlowEvent
        {
            SourceTransactionId = Guid.NewGuid(),
            Name = "Card Purchase",
            Date = new DateOnly(2026, 6, 10),
            Kind = TransactionKind.DebtIncrease,
            ToAccountId = creditCardAccount.Id,
            Amount = 250m,
            Currency = "CHF"
        };

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id);

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [purchaseBeforeClosing],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Single(events);
        Assert.Equal(250m, events[0].Amount);
    }

    [Fact]
    public void GenerateEvents_Should_NotIncludePurchasesAfterClosingDay()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: 0m);

        var purchaseAfterClosing = new CashFlowEvent
        {
            SourceTransactionId = Guid.NewGuid(),
            Name = "Card Purchase",
            Date = new DateOnly(2026, 6, 16),
            Kind = TransactionKind.DebtIncrease,
            ToAccountId = creditCardAccount.Id,
            Amount = 250m,
            Currency = "CHF"
        };

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id);

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [purchaseAfterClosing],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void GenerateEvents_Should_NotCreatePayment_WhenBalanceIsPositiveOrZero()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: 0m);

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id);

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        // Assert
        Assert.Empty(events);
    }

    [Fact]
    public void GenerateEvents_Should_MoveWeekendPaymentToNextBusinessDay()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: -1200m);

        // 2026-07-25 is Saturday.
        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id,
            startDate: new DateOnly(2026, 7, 1));

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 7, 1),
            new DateOnly(2026, 7, 31));

        // Assert
        Assert.Single(events);
        Assert.Equal(new DateOnly(2026, 7, 27), events[0].Date);
    }

    private static Account CreateBankAccount()
    {
        return new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 10000m,
            OpeningDate = new DateOnly(2026, 6, 1)
        };
    }

    private static Account CreateCreditCardAccount(decimal openingBalance)
    {
        return new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Name = "Visa",
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = new DateOnly(2026, 6, 1)
        };
    }

    private static CreditCardContract CreateCreditCardContract(
        Guid creditCardAccountId,
        Guid paymentAccountId,
        DateOnly? startDate = null)
    {
        return new CreditCardContract
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Name = "Visa",
            CreditCardAccountId = creditCardAccountId,
            PaymentAccountId = paymentAccountId,
            ClosingDayOfMonth = 15,
            PaymentDayOfMonth = 25,
            PaymentMethod = CreditCardPaymentMethod.AutomaticLsv,
            PaymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay,
            StartDate = startDate ?? new DateOnly(2026, 6, 1),
            IsActive = true
        };
    }

    [Fact]
    public void GenerateEvents_Should_NotRepeatOpeningDebtPayment_EveryMonth()
    {
        // Arrange
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: -1200m);

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id);

        var generator = new CreditCardPaymentEventGenerator();

        // Act
        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        // Assert
        Assert.Single(events);

        var payment = events.Single();

        Assert.Equal(new DateOnly(2026, 6, 25), payment.Date);
        Assert.Equal(1200m, payment.Amount);
        Assert.Equal(TransactionKind.DebtPayment, payment.Kind);
        Assert.Equal(mainAccount.Id, payment.FromAccountId);
        Assert.Equal(creditCardAccount.Id, payment.ToAccountId);
    }
}