using System.Text;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Csv;
using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.Core.Tests.Banking.Csv;

/// <summary>
/// The end-to-end CSV path, and above all the deduplication - which is where CSV differs from
/// MT940 and camt.053 in a way that can cost the user money in both directions.
/// </summary>
public sealed class CsvImportTests
{
    private static Account CreateAccount(string? iban = null)
    {
        return new Account
        {
            Name = "Privatkonto",
            Currency = "CHF",
            Iban = iban,
            OpeningBalance = 1000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };
    }

    private static BankStatementImportResult Import(
        byte[] fileBytes,
        Account account,
        IReadOnlyCollection<ImportedBankTransaction> existing,
        string? profileId = null)
    {
        return new BankStatementImportService().ImportCsv(new BankStatementImportRequest
        {
            FileBytes = fileBytes,
            FileName = "export.csv",
            Accounts = [account],
            ExistingImportedTransactions = existing,
            SelectedAccountId = account.Id,
            CsvProfileId = profileId,
            AsOfDate = new DateOnly(2026, 2, 1)
        });
    }

    private static byte[] Csv(params string[] lines)
    {
        return Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n");
    }

    [Fact]
    public void ImportCsv_MapsTheStatementOntoTheSelectedAccount()
    {
        var account = CreateAccount();

        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            account,
            []);

        Assert.True(result.CanImport);
        Assert.Equal(account.Id, result.AccountId);
        Assert.Equal(BankStatementImportService.CsvSourceFormat, result.Preview.SourceFormat);
        Assert.Equal(6, result.Preview.ParsedTransactionCount);
        Assert.NotNull(result.MergeResult);
        Assert.Equal(6, result.MergeResult.AddedCount);
    }

    [Fact]
    public void ImportCsv_MatchesTheAccountByTheIbanInThePreamble()
    {
        var account = CreateAccount("CH93 0076 2011 6238 5295 7");

        var result = new BankStatementImportService().ImportCsv(new BankStatementImportRequest
        {
            FileBytes = CsvFixture.ReadBytes(CsvFixture.WithPreamble),
            Accounts = [account],
            ExistingImportedTransactions = []
        });

        Assert.Equal(BankStatementAccountMatchStatus.MatchedByBankIdentifier, result.AccountMatchStatus);
        Assert.Equal(account.Id, result.AccountId);
    }

    [Fact]
    public void ImportCsv_AsksForAnAccountWhenTheFileNamesNoIban()
    {
        var result = new BankStatementImportService().ImportCsv(new BankStatementImportRequest
        {
            FileBytes = CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            Accounts = [CreateAccount()],
            ExistingImportedTransactions = []
        });

        Assert.True(result.RequiresAccountSelection);
        Assert.False(result.CanImport);
    }

    /// <summary>
    /// No balance column means no balance. Suggesting one derived from the transactions would
    /// overwrite a figure the user maintains by hand with a number computed from an arbitrary
    /// starting point.
    /// </summary>
    [Fact]
    public void ImportCsv_ProposesNoBalanceUpdateWhenTheFileHasNoBalanceColumn()
    {
        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            CreateAccount(),
            []);

        Assert.Null(result.SuggestedBalanceUpdate);
        Assert.False(result.Preview.ReconciliationAvailable);
    }

    [Fact]
    public void ImportCsv_ProposesABalanceUpdateWhenTheFileDoesCarryABalance()
    {
        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.DebitCreditColumns),
            CreateAccount(),
            []);

        Assert.NotNull(result.SuggestedBalanceUpdate);
        Assert.Equal(19904.95m, result.SuggestedBalanceUpdate.Balance);
        Assert.True(result.Preview.ReconciliationBalanced);
    }

    /// <summary>
    /// The most important property of the whole feature: importing the same export twice must
    /// add nothing the second time. Users re-download statements constantly.
    /// </summary>
    [Fact]
    public void ImportCsv_ReImportingTheSameFileAddsNothing()
    {
        var account = CreateAccount();
        var bytes = CsvFixture.ReadBytes(CsvFixture.SwissApostrophe);

        var first = Import(bytes, account, []);

        Assert.NotNull(first.MergeResult);

        var second = Import(bytes, account, first.MergeResult.MergedTransactions);

        Assert.NotNull(second.MergeResult);
        Assert.Equal(0, second.MergeResult.AddedCount);
        Assert.Equal(6, second.MergeResult.SkippedDuplicateCount);
        Assert.Equal(6, second.MergeResult.MergedTransactions.Count);
    }

    /// <summary>
    /// Two coffees on the same afternoon are two transactions. Same date, same 4.50, same shop -
    /// and with no transaction id in the file, a plain content hash cannot tell them apart.
    /// Collapsing them loses 4.50 from the plan, silently.
    /// </summary>
    [Fact]
    public void ImportCsv_KeepsTwoLegitimatelyIdenticalRows()
    {
        var account = CreateAccount();

        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            account,
            []);

        Assert.NotNull(result.MappingResult);

        var coffees = result.MappingResult.Transactions
            .Where(x => x.SignedAmount == -4.50m)
            .ToList();

        Assert.Equal(2, coffees.Count);
        Assert.Equal(2, coffees.Select(x => x.DeduplicationKey).Distinct().Count());
    }

    /// <summary>
    /// And a third one next month is genuinely new. The rank among identical siblings makes both
    /// halves work: two stay two, three become three.
    /// </summary>
    [Fact]
    public void ImportCsv_AddsAThirdIdenticalTransactionWhenTheBankReportsOne()
    {
        var account = CreateAccount();

        var twoCoffees = Csv(
            "Datum;Buchungstext;Betrag",
            "16.01.2026;COOP PRONTO;-4.50",
            "16.01.2026;COOP PRONTO;-4.50");

        var threeCoffees = Csv(
            "Datum;Buchungstext;Betrag",
            "16.01.2026;COOP PRONTO;-4.50",
            "16.01.2026;COOP PRONTO;-4.50",
            "16.01.2026;COOP PRONTO;-4.50");

        var first = Import(twoCoffees, account, []);

        Assert.NotNull(first.MergeResult);
        Assert.Equal(2, first.MergeResult.AddedCount);

        var second = Import(threeCoffees, account, first.MergeResult.MergedTransactions);

        Assert.NotNull(second.MergeResult);
        Assert.Equal(1, second.MergeResult.AddedCount);
        Assert.Equal(2, second.MergeResult.SkippedDuplicateCount);
        Assert.Equal(3, second.MergeResult.MergedTransactions.Count);
    }

    /// <summary>
    /// The reason the rank is counted among identical siblings and not taken from the row
    /// number. A bank inserting a transaction above ours shifts every row index below it; if the
    /// key depended on that, next month's overlapping export would re-add the entire statement.
    /// </summary>
    [Fact]
    public void ImportCsv_KeysSurviveARowBeingInsertedAboveThem()
    {
        var account = CreateAccount();

        var january = Csv(
            "Datum;Buchungstext;Betrag",
            "16.01.2026;COOP PRONTO;-4.50",
            "17.01.2026;MIGROS;-32.10",
            "18.01.2026;SBB;-8.80");

        var januaryWithAnExtraRowInTheMiddle = Csv(
            "Datum;Buchungstext;Betrag",
            "16.01.2026;COOP PRONTO;-4.50",
            "16.01.2026;KIOSK;-3.20",
            "17.01.2026;MIGROS;-32.10",
            "18.01.2026;SBB;-8.80");

        var first = Import(january, account, []);

        Assert.NotNull(first.MergeResult);

        var second = Import(
            januaryWithAnExtraRowInTheMiddle,
            account,
            first.MergeResult.MergedTransactions);

        Assert.NotNull(second.MergeResult);
        Assert.Equal(1, second.MergeResult.AddedCount);
        Assert.Equal(3, second.MergeResult.SkippedDuplicateCount);
        Assert.Equal(4, second.MergeResult.MergedTransactions.Count);
    }

    /// <summary>
    /// A reference column must not become the deduplication key. A standing order carries the
    /// same QR reference every month, so keying on it would make February's rent a duplicate of
    /// January's and drop it - which is exactly the money-losing failure the content hash avoids.
    /// </summary>
    [Fact]
    public void ImportCsv_DoesNotKeyOnARepeatingPaymentReference()
    {
        var account = CreateAccount();

        var january = Csv(
            "Datum;Buchungstext;Referenz;Betrag",
            "01.01.2026;Miete Januar;RF18539007547034;-2400.00");

        var february = Csv(
            "Datum;Buchungstext;Referenz;Betrag",
            "01.02.2026;Miete Februar;RF18539007547034;-2400.00");

        var first = Import(january, account, []);

        Assert.NotNull(first.MergeResult);

        var second = Import(february, account, first.MergeResult.MergedTransactions);

        Assert.NotNull(second.MergeResult);
        Assert.Equal(1, second.MergeResult.AddedCount);
        Assert.Equal(2, second.MergeResult.MergedTransactions.Count);
    }

    [Fact]
    public void ImportCsv_KeepsTheReferenceForDisplayEvenThoughItDoesNotKey()
    {
        var account = CreateAccount();

        var result = Import(
            Csv(
                "Datum;Buchungstext;Referenz;Betrag",
                "01.01.2026;Miete Januar;RF18539007547034;-2400.00"),
            account,
            []);

        Assert.NotNull(result.MappingResult);

        var transaction = Assert.Single(result.MappingResult.Transactions);

        Assert.Null(transaction.BankReference);
        Assert.Equal("RF18539007547034", transaction.CustomerReference);
        Assert.StartsWith("fallback:", transaction.DeduplicationKey, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportCsv_CarriesTheColumnMappingOnThePreview()
    {
        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            CreateAccount(),
            []);

        var csv = result.Preview.Csv;

        Assert.NotNull(csv);
        Assert.Equal(CsvStatementProfiles.AutoId, csv.ProfileId);
        Assert.Equal(";", csv.Delimiter);
        Assert.Equal("d.M.yyyy", csv.DateFormat);

        var amountColumn = Assert.Single(
            csv.Columns,
            x => x.Role == CsvColumnRole.Amount);

        Assert.Equal(3, amountColumn.ColumnNumber);
        Assert.Equal("Betrag", amountColumn.Header);
    }

    [Fact]
    public void ImportCsv_CarriesTheUnreadableRowsOnThePreview()
    {
        var result = Import(
            CsvFixture.ReadBytes(CsvFixture.UnreadableRows),
            CreateAccount(),
            []);

        Assert.NotNull(result.Preview.Csv);
        Assert.Equal(2, result.Preview.Csv.RowIssueCount);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);
    }

    [Fact]
    public void Import_RoutesACsvFileToTheCsvPath()
    {
        var results = new BankStatementImportService().Import(new BankStatementImportRequest
        {
            FileBytes = CsvFixture.ReadBytes(CsvFixture.SwissApostrophe),
            FileName = "export.csv",
            Accounts = [CreateAccount()],
            ExistingImportedTransactions = []
        });

        var result = Assert.Single(results);

        Assert.Equal(BankStatementImportService.CsvSourceFormat, result.Preview.SourceFormat);
    }

    [Fact]
    public void DetectSourceFormat_TellsCsvApartFromMt940AndCamt()
    {
        Assert.Equal(
            BankStatementImportService.CsvSourceFormat,
            BankStatementImportService.DetectSourceFormat(
                CsvFixture.ReadBytes(CsvFixture.SwissApostrophe)));

        Assert.Equal(
            BankStatementImportService.Mt940SourceFormat,
            BankStatementImportService.DetectSourceFormat(
                Encoding.UTF8.GetBytes(
                    ":20:REF\r\n:25:CH2100210210108311400\r\n:60F:C260101CHF4042,62\r\n")));
    }

    /// <summary>
    /// A file that is neither camt, nor MT940, nor recognisably CSV keeps going to the MT940
    /// path, so it produces exactly the error it produced before CSV import existed.
    /// </summary>
    [Fact]
    public void DetectSourceFormat_LeavesUnrecognisableFilesOnTheMt940Path()
    {
        Assert.Equal(
            BankStatementImportService.Mt940SourceFormat,
            BankStatementImportService.DetectSourceFormat(
                CsvFixture.ReadBytes(CsvFixture.Malformed)));
    }

    [Fact]
    public void ImportCsv_SurfacesAParseFailureAsAnActionableMessage()
    {
        var exception = Assert.Throws<CsvParseException>(
            () => Import(CsvFixture.ReadBytes(CsvFixture.MalformedDates), CreateAccount(), []));

        Assert.Contains("could not be read", exception.Message, StringComparison.Ordinal);
    }
}
