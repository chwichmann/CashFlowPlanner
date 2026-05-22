using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountBankIdentifierMatcherTests
{
    [Fact]
    public void FindByMt940AccountId_ReturnsMatchingAccount()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = "CH230021021010831140E",
                    BankName = "UBS"
                }
            ]
        };

        var accounts = new[]
        {
            account
        };

        var result = AccountBankIdentifierMatcher.FindByMt940AccountId(
            accounts,
            "CH230021021010831140E",
            "UBS");

        Assert.Same(account, result);
    }

    [Fact]
    public void FindByMt940AccountId_NormalizesSpacesAndCase()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = "CH23 0021 0210 1083 1140E",
                    BankName = "UBS"
                }
            ]
        };

        var accounts = new[]
        {
            account
        };

        var result = AccountBankIdentifierMatcher.FindByMt940AccountId(
            accounts,
            "ch230021021010831140e",
            "ubs");

        Assert.Same(account, result);
    }

    [Fact]
    public void FindByMt940AccountId_ReturnsNull_WhenNoMatchExists()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Savings Account",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 500m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = "CH9800210210109430M1A",
                    BankName = "UBS"
                }
            ]
        };

        var accounts = new[]
        {
            account
        };

        var result = AccountBankIdentifierMatcher.FindByMt940AccountId(
            accounts,
            "CH230021021010831140E",
            "UBS");

        Assert.Null(result);
    }

    [Fact]
    public void HasMt940AccountId_ReturnsTrue_WhenIdentifierMatches()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = "CH230021021010831140E",
                    BankName = "UBS"
                }
            ]
        };

        var result = AccountBankIdentifierMatcher.HasMt940AccountId(
            account,
            "CH230021021010831140E",
            "UBS");

        Assert.True(result);
    }

    [Fact]
    public void WithAddedIdentifierIfMissing_AddsIdentifier()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19)
        };

        var updated = AccountBankIdentifierMatcher.WithAddedIdentifierIfMissing(
            account,
            new AccountBankIdentifier
            {
                Type = AccountBankIdentifierType.Mt940AccountId,
                Value = "CH230021021010831140E",
                BankName = "UBS"
            });

        Assert.Single(updated.BankIdentifiers);
        Assert.Equal(
            "CH230021021010831140E",
            updated.BankIdentifiers[0].Value);
    }

    [Fact]
    public void WithAddedIdentifierIfMissing_DoesNotAddDuplicate()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = "CH230021021010831140E",
                    BankName = "UBS"
                }
            ]
        };

        var updated = AccountBankIdentifierMatcher.WithAddedIdentifierIfMissing(
            account,
            new AccountBankIdentifier
            {
                Type = AccountBankIdentifierType.Mt940AccountId,
                Value = "CH23 0021 0210 1083 1140E",
                BankName = "UBS"
            });

        Assert.Single(updated.BankIdentifiers);
    }
}