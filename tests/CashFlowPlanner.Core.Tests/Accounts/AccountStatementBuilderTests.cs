using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountStatementBuilderTests
{
    [Fact]
    public void Build_IncomingAndOutgoingEvents_ReturnsRunningBalance()
    {
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1_000m,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var events = new List<CashFlowEvent>
        {
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Salary",
                Date = new DateOnly(2026, 1, 25),
                Kind = TransactionKind.ExternalIncome,
                ToAccountId = accountId,
                Amount = 5_000m,
                Currency = "CHF",
                Priority = 10
            },
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Rent",
                Date = new DateOnly(2026, 1, 28),
                Kind = TransactionKind.ExternalExpense,
                FromAccountId = accountId,
                Amount = 2_000m,
                Currency = "CHF",
                Priority = 20
            }
        };

        var rows = AccountStatementBuilder.Build(
            account,
            events,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        Assert.Equal(2, rows.Count);

        Assert.Equal(5_000m, rows[0].Incoming);
        Assert.Null(rows[0].Outgoing);
        Assert.Equal(6_000m, rows[0].Balance);

        Assert.Null(rows[1].Incoming);
        Assert.Equal(2_000m, rows[1].Outgoing);
        Assert.Equal(4_000m, rows[1].Balance);
    }

    [Fact]
    public void Build_WhenStatementStartsBeforeOpeningDate_AppliesOpeningBalanceBeforeFirstEvent()
    {
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            Name = "Stockwerkeigentum",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 748.60m,
            OpeningDate = new DateOnly(2026, 5, 15),
            IsActive = true
        };

        var events = new List<CashFlowEvent>
    {
        new()
        {
            SourceTransactionId = Guid.NewGuid(),
            Name = "Stockwerkeigentum",
            Date = new DateOnly(2026, 5, 26),
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = accountId,
            Amount = 695m,
            Currency = "CHF",
            Priority = 100,
            Category = "Wohnen"
        }
    };

        var rows = AccountStatementBuilder.Build(
            account,
            events,
            new DateOnly(2026, 5, 11),
            new DateOnly(2026, 12, 31));

        Assert.Equal(2, rows.Count);

        Assert.Equal("Opening balance", rows[0].Title);
        Assert.Equal(748.60m, rows[0].Balance);

        Assert.Equal("Stockwerkeigentum", rows[1].Title);
        Assert.Equal(695m, rows[1].Incoming);
        Assert.Equal(1443.60m, rows[1].Balance);
    }

    [Fact]
    public void Build_DebtIncrease_ReducesTheBalance_LikeTheSimulationEngine()
    {
        // H5: the statement applied "To => +, From => -" without looking at
        // TransactionKind, so a DebtIncrease of 500 showed +500 here and -500 in
        // the engine for the very same event.
        var creditCard = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: 0m,
            openingDate: new DateOnly(2026, 6, 1));

        var purchase = TestPlanBuilder.DebtIncrease(
            debtAccountId: creditCard.Id,
            amount: 500m,
            schedule: TestPlanBuilder.Once(new DateOnly(2026, 6, 10)),
            name: "Card purchase");

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [creditCard],
            transactions: [purchase],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var result = new SimulationEngine().Simulate(plan);

        var rows = AccountStatementBuilder.Build(
            creditCard,
            result.Events,
            new DateOnly(2026, 6, 1),
            new DateOnly(2026, 6, 30));

        var row = Assert.Single(rows);

        Assert.Equal(-500m, row.Balance);
        Assert.Equal(500m, row.Outgoing);
        Assert.Null(row.Incoming);

        // The statement and the engine must agree on the closing balance.
        Assert.Equal(
            result.GetBalance(creditCard.Id, new DateOnly(2026, 6, 30)),
            rows[^1].Balance);
    }

    [Theory]
    [InlineData(TransactionKind.ExternalIncome)]
    [InlineData(TransactionKind.ExternalExpense)]
    [InlineData(TransactionKind.InternalTransfer)]
    [InlineData(TransactionKind.DebtIncrease)]
    [InlineData(TransactionKind.DebtPayment)]
    public void Build_ClosingBalance_AlwaysMatchesTheSimulationEngine(TransactionKind kind)
    {
        var bankAccount = TestPlanBuilder.CreateBankAccount(
            openingBalance: 5_000m,
            openingDate: new DateOnly(2026, 6, 1));

        var otherAccount = TestPlanBuilder.CreateCreditCardAccount(
            openingBalance: -2_000m,
            openingDate: new DateOnly(2026, 6, 1));

        var schedule = TestPlanBuilder.Once(new DateOnly(2026, 6, 10));

        var transaction = kind switch
        {
            TransactionKind.ExternalIncome =>
                TestPlanBuilder.ExternalIncome(bankAccount.Id, 750m, schedule),
            TransactionKind.ExternalExpense =>
                TestPlanBuilder.ExternalExpense(bankAccount.Id, 750m, schedule),
            TransactionKind.InternalTransfer =>
                TestPlanBuilder.InternalTransfer(bankAccount.Id, otherAccount.Id, 750m, schedule),
            TransactionKind.DebtIncrease =>
                TestPlanBuilder.DebtIncrease(otherAccount.Id, 750m, schedule),
            _ =>
                TestPlanBuilder.DebtPayment(bankAccount.Id, otherAccount.Id, 750m, schedule)
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bankAccount, otherAccount],
            transactions: [transaction],
            startDate: new DateOnly(2026, 6, 1),
            endDate: new DateOnly(2026, 6, 30));

        var result = new SimulationEngine().Simulate(plan);

        foreach (var account in new[] { bankAccount, otherAccount })
        {
            var rows = AccountStatementBuilder.Build(
                account,
                result.Events,
                new DateOnly(2026, 6, 1),
                new DateOnly(2026, 6, 30));

            var engineBalance = result.GetBalance(account.Id, new DateOnly(2026, 6, 30));

            var statementBalance = rows.Count == 0
                ? account.OpeningBalance
                : rows[^1].Balance;

            Assert.Equal(engineBalance, statementBalance);
        }
    }

    [Fact]
    public void Build_SameDateRows_AreOrderedSoTheRunningBalanceReconciles()
    {
        // H4: the rows were re-sorted by (ValutaDate, Title) AFTER the running
        // balance had been accumulated in (Date, Priority, Name) order, so the
        // balance column no longer matched the rows next to it.
        var accountId = Guid.NewGuid();

        var account = new Account
        {
            Id = accountId,
            Name = "Main Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1_000m,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true
        };

        var events = new List<CashFlowEvent>
        {
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Zebra salary",
                Date = new DateOnly(2026, 1, 15),
                Kind = TransactionKind.ExternalIncome,
                ToAccountId = accountId,
                Amount = 1_000m,
                Currency = "CHF",
                Priority = 10
            },
            new()
            {
                SourceTransactionId = Guid.NewGuid(),
                Name = "Alpha rent",
                Date = new DateOnly(2026, 1, 15),
                Kind = TransactionKind.ExternalExpense,
                FromAccountId = accountId,
                Amount = 500m,
                Currency = "CHF",
                Priority = 100
            }
        };

        var rows = AccountStatementBuilder.Build(
            account,
            events,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 31));

        Assert.Equal(2, rows.Count);

        Assert.Equal("Zebra salary", rows[0].Title);
        Assert.Equal(2_000m, rows[0].Balance);

        Assert.Equal("Alpha rent", rows[1].Title);
        Assert.Equal(1_500m, rows[1].Balance);
    }
}