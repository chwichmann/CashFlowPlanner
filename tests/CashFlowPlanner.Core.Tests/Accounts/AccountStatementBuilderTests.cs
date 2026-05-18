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
}