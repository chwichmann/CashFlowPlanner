using System.Text;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.Core.Tests.Banking.Import;

public sealed class BankStatementImportServiceTests
{
    [Fact]
    public void ImportMt940_MatchesAccountByMt940Identifier_AndMergesTransactions()
    {
        var account = CreateAccountWithMt940Identifier(
            "CH230021021010831140E");

        var service = new BankStatementImportService();

        var result = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.False(result.RequiresAccountSelection);
        Assert.True(result.CanImport);
        Assert.Equal(BankStatementAccountMatchStatus.MatchedByBankIdentifier, result.AccountMatchStatus);
        Assert.Equal(account.Id, result.AccountId);
        Assert.NotNull(result.MappingResult);
        Assert.NotNull(result.MergeResult);
        Assert.NotNull(result.SuggestedBalanceUpdate);

        Assert.Equal("CH230021021010831140E", result.Preview.BankAccountIdentifier);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);
        Assert.True(result.Preview.ReconciliationAvailable);
        Assert.True(result.Preview.ReconciliationBalanced);

        Assert.Equal(2, result.MergeResult.AddedCount);
        Assert.Equal(0, result.MergeResult.SkippedDuplicateCount);
        Assert.Equal(2, result.MergeResult.MergedTransactions.Count);

        Assert.Equal(4978.37m, result.SuggestedBalanceUpdate.Balance);
        Assert.Equal(new DateOnly(2026, 1, 23), result.SuggestedBalanceUpdate.BalanceDate);
        Assert.True(result.SuggestedBalanceUpdate.ClosingBalanceDateLooksSuspicious);
    }

    [Fact]
    public void ImportMt940_ReturnsAccountSelectionRequired_WhenNoIdentifierMatchAndNoManualAccountSelected()
    {
        var account = CreateAccountWithMt940Identifier(
            "DIFFERENT-ID");

        var service = new BankStatementImportService();

        var result = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.True(result.RequiresAccountSelection);
        Assert.False(result.CanImport);
        Assert.Equal(BankStatementAccountMatchStatus.NotMatched, result.AccountMatchStatus);
        Assert.Null(result.AccountId);
        Assert.Null(result.MappingResult);
        Assert.Null(result.MergeResult);
        Assert.Null(result.SuggestedBalanceUpdate);
        Assert.Equal("CH230021021010831140E", result.BankAccountIdentifierToRemember);

        Assert.Equal("CH230021021010831140E", result.Preview.BankAccountIdentifier);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);
    }

    [Fact]
    public void ImportMt940_UsesSelectedAccount_WhenNoIdentifierMatch()
    {
        var account = CreateAccountWithMt940Identifier(
            "DIFFERENT-ID");

        var service = new BankStatementImportService();

        var result = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            SelectedAccountId = account.Id,
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.False(result.RequiresAccountSelection);
        Assert.True(result.CanImport);
        Assert.Equal(BankStatementAccountMatchStatus.SelectedManually, result.AccountMatchStatus);
        Assert.Equal(account.Id, result.AccountId);
        Assert.NotNull(result.MappingResult);
        Assert.NotNull(result.MergeResult);
        Assert.NotNull(result.SuggestedBalanceUpdate);
        Assert.Equal("CH230021021010831140E", result.BankAccountIdentifierToRemember);
    }

    [Fact]
    public void ImportMt940_SkipsDuplicateTransactions_WhenExistingImportedTransactionsContainSameBankReference()
    {
        var account = CreateAccountWithMt940Identifier(
            "CH230021021010831140E");

        var service = new BankStatementImportService();

        var firstResult = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.NotNull(firstResult.MergeResult);

        var secondResult = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = firstResult.MergeResult.MergedTransactions,
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.NotNull(secondResult.MergeResult);
        Assert.Equal(0, secondResult.MergeResult.AddedCount);
        Assert.Equal(2, secondResult.MergeResult.SkippedDuplicateCount);
        Assert.Equal(2, secondResult.MergeResult.MergedTransactions.Count);
    }

    [Fact]
    public void ImportMt940_AddsNewTransactions_WhenSecondFileContainsAdditionalBankReference()
    {
        var account = CreateAccountWithMt940Identifier(
            "CH230021021010831140E");

        var service = new BankStatementImportService();

        var firstResult = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
            FileName = "transactions-1.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.NotNull(firstResult.MergeResult);

        var secondResult = service.ImportMt940(new BankStatementImportRequest
        {
            FileBytes = Encoding.UTF8.GetBytes(CreateStatementWithAdditionalTransaction()),
            FileName = "transactions-2.mt940",
            Accounts = [account],
            ExistingImportedTransactions = firstResult.MergeResult.MergedTransactions,
            AsOfDate = new DateOnly(2026, 5, 19)
        });

        Assert.NotNull(secondResult.MergeResult);
        Assert.Equal(1, secondResult.MergeResult.AddedCount);
        Assert.Equal(2, secondResult.MergeResult.SkippedDuplicateCount);
        Assert.Equal(3, secondResult.MergeResult.MergedTransactions.Count);
    }

    [Fact]
    public void ImportMt940_Throws_WhenSelectedAccountDoesNotExist()
    {
        var account = CreateAccountWithMt940Identifier(
            "DIFFERENT-ID");

        var service = new BankStatementImportService();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.ImportMt940(new BankStatementImportRequest
            {
                FileBytes = Encoding.UTF8.GetBytes(CreateStatement()),
                FileName = "transactions.mt940",
                Accounts = [account],
                ExistingImportedTransactions = [],
                SelectedAccountId = Guid.NewGuid(),
                AsOfDate = new DateOnly(2026, 5, 19)
            }));

        Assert.Contains("Selected account", exception.Message);
    }

    [Fact]
    public void ImportMt940_Throws_WhenFileIsEmpty()
    {
        var service = new BankStatementImportService();

        var exception = Assert.Throws<InvalidOperationException>(() =>
            service.ImportMt940(new BankStatementImportRequest
            {
                FileBytes = [],
                FileName = "empty.mt940",
                Accounts = [],
                ExistingImportedTransactions = [],
                AsOfDate = new DateOnly(2026, 5, 19)
            }));

        Assert.Contains("empty", exception.Message);
    }

    private static Account CreateAccountWithMt940Identifier(string mt940AccountId)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = "UBS Private Account",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 5, 19),
            BankName = "UBS",
            BankIdentifiers =
            [
                new AccountBankIdentifier
                {
                    Type = AccountBankIdentifierType.Mt940AccountId,
                    Value = mt940AccountId,
                    BankName = "UBS"
                }
            ]
        };
    }

    private static string CreateStatement()
    {
        return
            """
            :20:02100010831101
            :25:CH230021021010831140E
            :28C:142/1
            :60F:C260101CHF4042,62
            :61:2601050105D40,NMSCNONREF//9910005GK0615030
            Zahlung UBS TWINT
            :86:K70?BAZG VIA-WEBSHOP Zahlung UBS TWINT
            :61:2601230123C975,75NTRFNONREF//9999023ZC7856428
            Salary
            :86:Z32?Example Employer Salary
            :62F:C261231CHF4978,37
            """;
    }

    private static string CreateStatementWithAdditionalTransaction()
    {
        return
            """
            :20:02100010831101
            :25:CH230021021010831140E
            :28C:142/1
            :60F:C260101CHF4042,62
            :61:2601050105D40,NMSCNONREF//9910005GK0615030
            Zahlung UBS TWINT
            :86:K70?BAZG VIA-WEBSHOP Zahlung UBS TWINT
            :61:2601230123C975,75NTRFNONREF//9999023ZC7856428
            Salary
            :86:Z32?Example Employer Salary
            :61:2601240124D10,NTRFNONREF//NEW-REF-001
            Additional payment
            :86:Z44?Additional payment
            :62F:C261231CHF4968,37
            """;
    }
}