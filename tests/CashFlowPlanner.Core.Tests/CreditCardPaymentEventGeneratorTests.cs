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

    [Theory]
    // Payment day after the closing day: the payment settles the statement that
    // closed earlier in the same month.
    [InlineData(15, 25, 2026, 6, 25)]
    // Payment day at or before the closing day: the payment can only settle the
    // statement in the FOLLOWING month, never before the statement closes.
    [InlineData(25, 5, 2026, 7, 5)]
    [InlineData(25, 1, 2026, 7, 1)]
    // Closing day clamped to 30 June; payment rolls to 30 July.
    [InlineData(31, 30, 2026, 7, 30)]
    public void GenerateEvents_Should_DatePaymentAfterTheStatementItSettles(
        int closingDayOfMonth,
        int paymentDayOfMonth,
        int expectedYear,
        int expectedMonth,
        int expectedDay)
    {
        var mainAccount = CreateBankAccount();
        var creditCardAccount = CreateCreditCardAccount(openingBalance: -1200m);

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id,
            closingDayOfMonth: closingDayOfMonth,
            paymentDayOfMonth: paymentDayOfMonth,
            paymentBusinessDayAdjustment: BusinessDayAdjustment.None);

        var generator = new CreditCardPaymentEventGenerator();

        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 8, 31));

        var payment = Assert.Single(events);

        Assert.Equal(
            new DateOnly(expectedYear, expectedMonth, expectedDay),
            payment.Date);
    }

    [Fact]
    public void GenerateEvents_Should_ClampRolledPaymentDayToTheFollowingMonth()
    {
        // Closing 30 January, payment day 29 => rolls into February,
        // which has only 28 days in 2026.
        var mainAccount = CreateBankAccount(openingDate: new DateOnly(2026, 1, 1));
        var creditCardAccount = CreateCreditCardAccount(
            openingBalance: -1200m,
            openingDate: new DateOnly(2026, 1, 1));

        var contract = CreateCreditCardContract(
            creditCardAccount.Id,
            mainAccount.Id,
            startDate: new DateOnly(2026, 1, 1),
            closingDayOfMonth: 30,
            paymentDayOfMonth: 29,
            paymentBusinessDayAdjustment: BusinessDayAdjustment.None);

        var generator = new CreditCardPaymentEventGenerator();

        var events = generator.GenerateEvents(
            [contract],
            [mainAccount, creditCardAccount],
            [],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        var payment = Assert.Single(events);

        Assert.Equal(new DateOnly(2026, 2, 28), payment.Date);
    }

    private static Account CreateBankAccount(DateOnly? openingDate = null)
    {
        return new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000001"),
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 10000m,
            OpeningDate = openingDate ?? new DateOnly(2026, 6, 1)
        };
    }

    private static Account CreateCreditCardAccount(
        decimal openingBalance,
        DateOnly? openingDate = null)
    {
        return new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000003"),
            Name = "Visa",
            Type = AccountType.CreditCard,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = openingDate ?? new DateOnly(2026, 6, 1)
        };
    }

    private static CreditCardContract CreateCreditCardContract(
        Guid creditCardAccountId,
        Guid paymentAccountId,
        DateOnly? startDate = null,
        int closingDayOfMonth = 15,
        int paymentDayOfMonth = 25,
        BusinessDayAdjustment paymentBusinessDayAdjustment = BusinessDayAdjustment.NextBusinessDay)
    {
        return new CreditCardContract
        {
            Id = Guid.Parse("40000000-0000-0000-0000-000000000001"),
            Name = "Visa",
            CreditCardAccountId = creditCardAccountId,
            PaymentAccountId = paymentAccountId,
            ClosingDayOfMonth = closingDayOfMonth,
            PaymentDayOfMonth = paymentDayOfMonth,
            PaymentMethod = CreditCardPaymentMethod.AutomaticLsv,
            PaymentBusinessDayAdjustment = paymentBusinessDayAdjustment,
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