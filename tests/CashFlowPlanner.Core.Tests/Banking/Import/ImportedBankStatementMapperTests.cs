using CashFlowPlanner.Core.Banking.Import;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Tests.Banking.Import;

public sealed class ImportedBankStatementMapperTests
{
    [Fact]
    public void MapFromMt940_CreatesBatchAndTransactions()
    {
        var parser = new Mt940Parser();
        var statement = parser.Parse(CreateStatement());
        var mapper = new ImportedBankStatementMapper();

        var accountId = Guid.NewGuid();

        var result = mapper.MapFromMt940(
            statement,
            accountId,
            "FILE-FINGERPRINT",
            "test.mt940",
            new DateTime(2026, 5, 19, 12, 0, 0, DateTimeKind.Utc));

        Assert.Equal(accountId, result.Batch.AccountId);
        Assert.Equal("MT940", result.Batch.SourceFormat);
        Assert.Equal("test.mt940", result.Batch.FileName);
        Assert.Equal("FILE-FINGERPRINT", result.Batch.FileFingerprint);
        Assert.Equal("CH230021021010831140E", result.Batch.BankAccountIdentifier);
        Assert.Equal("02100010831101", result.Batch.TransactionReference);
        Assert.Equal("142/1", result.Batch.StatementNumber);
        Assert.Equal(new DateOnly(2026, 1, 1), result.Batch.OpeningBalanceDate);
        Assert.Equal(4042.62m, result.Batch.OpeningBalance);
        Assert.Equal(new DateOnly(2026, 12, 31), result.Batch.ClosingBalanceDate);
        Assert.Equal(4978.37m, result.Batch.ClosingBalance);
        Assert.Equal("CHF", result.Batch.Currency);
        Assert.Equal(2, result.Batch.ParsedTransactionCount);
        Assert.True(result.Batch.ReconciliationAvailable);
        Assert.True(result.Batch.ReconciliationBalanced);
        Assert.Equal(0m, result.Batch.ReconciliationDifference);

        Assert.Equal(2, result.Transactions.Count);

        var first = result.Transactions[0];

        Assert.Equal(accountId, first.AccountId);
        Assert.Equal(result.Batch.Id, first.ImportBatchId);
        Assert.Equal("MT940", first.SourceFormat);
        Assert.Equal("CH230021021010831140E", first.BankAccountIdentifier);
        Assert.Equal(new DateOnly(2026, 1, 5), first.ValueDate);
        Assert.Equal(new DateOnly(2026, 1, 5), first.BookingDate);
        Assert.Equal(-40m, first.SignedAmount);
        Assert.Equal(40m, first.Amount);
        Assert.True(first.IsOutgoing);
        Assert.False(first.IsIncoming);
        Assert.Equal("CHF", first.Currency);
        Assert.Equal("NMSC", first.TransactionCode);
        Assert.Equal("K70", first.Structured86Code);
        Assert.Equal("9910005GK0615030", first.BankReference);
        Assert.False(string.IsNullOrWhiteSpace(first.DeduplicationKey));
        Assert.Equal(ImportedTransactionMatchStatus.Unmatched, first.MatchStatus);
    }

    [Fact]
    public void MapFromMt940_UsesTransactionDateRange()
    {
        var parser = new Mt940Parser();
        var statement = parser.Parse(CreateStatement());
        var mapper = new ImportedBankStatementMapper();

        var result = mapper.MapFromMt940(
            statement,
            Guid.NewGuid(),
            "FILE-FINGERPRINT");

        Assert.Equal(new DateOnly(2026, 1, 5), result.Batch.FirstTransactionDate);
        Assert.Equal(new DateOnly(2026, 1, 23), result.Batch.LastTransactionDate);
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
}