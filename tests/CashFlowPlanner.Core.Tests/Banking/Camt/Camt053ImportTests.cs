using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.Core.Tests.Banking.Camt;

/// <summary>
/// End-to-end tests over the shared pipeline: parse -> map -> deduplicate -> account match ->
/// merge, driven through <see cref="BankStatementImportService"/> exactly as the UI drives it.
/// </summary>
public sealed class Camt053ImportTests
{
    private const string PrivateIban = "CH2100210210108311400";
    private const string SavingsIban = "CH5604835012345678009";
    private const string EuroIban = "CH9300762011623852957";

    private static readonly DateOnly AsOfDate = new(2026, 6, 1);

    private static BankStatementImportRequest CreateRequest(
        string fixtureName,
        IReadOnlyCollection<Account> accounts,
        IReadOnlyCollection<ImportedBankTransaction>? existingTransactions = null,
        Guid? selectedAccountId = null)
    {
        return new BankStatementImportRequest
        {
            FileBytes = CamtFixture.ReadBytes(fixtureName),
            FileName = fixtureName,
            Accounts = accounts,
            ExistingImportedTransactions = existingTransactions ?? [],
            SelectedAccountId = selectedAccountId,
            AsOfDate = AsOfDate
        };
    }

    private static Account CreateAccount(
        string name,
        string? iban = null,
        AccountBankIdentifier? bankIdentifier = null,
        string currency = "CHF")
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = AccountType.BankAccount,
            Currency = currency,
            OpeningBalance = 0m,
            OpeningDate = new DateOnly(2026, 1, 1),
            BankName = "UBS",
            Iban = iban,
            BankIdentifiers = bankIdentifier is null
                ? []
                : [bankIdentifier]
        };
    }

    [Fact]
    public void ImportCamt053_MatchesByStoredIbanIdentifier_AndMergesTransactions()
    {
        var account = CreateAccount(
            "UBS Privatkonto",
            bankIdentifier: new AccountBankIdentifier
            {
                Type = AccountBankIdentifierType.Iban,
                Value = PrivateIban,
                BankName = "UBS"
            });

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]));

        var result = Assert.Single(results);

        Assert.Equal(BankStatementAccountMatchStatus.MatchedByBankIdentifier, result.AccountMatchStatus);
        Assert.Equal(account.Id, result.AccountId);
        Assert.True(result.CanImport);
        Assert.False(result.RequiresAccountSelection);

        Assert.Equal("CAMT053", result.Preview.SourceFormat);
        Assert.Equal(PrivateIban, result.Preview.BankAccountIdentifier);
        Assert.Equal("STMT-2026-004", result.Preview.TransactionReference);
        Assert.Equal("4", result.Preview.StatementNumber);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);
        Assert.Equal(935.75m, result.Preview.TransactionNetAmount);
        Assert.Equal(4042.62m, result.Preview.OpeningBalance);
        Assert.Equal(4978.37m, result.Preview.ClosingBalance);
        Assert.True(result.Preview.ReconciliationAvailable);
        Assert.True(result.Preview.ReconciliationBalanced);

        Assert.NotNull(result.MergeResult);
        Assert.Equal(2, result.MergeResult.AddedCount);
        Assert.Equal(0, result.MergeResult.SkippedDuplicateCount);
    }

    [Fact]
    public void ImportCamt053_MatchesByTheAccountsOwnIbanField()
    {
        // An account the user filled in by hand matches with no import-specific setup at all.
        var account = CreateAccount("UBS Privatkonto", iban: "CH21 0021 0210 1083 1140 0");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]));

        var result = Assert.Single(results);

        Assert.Equal(BankStatementAccountMatchStatus.MatchedByBankIdentifier, result.AccountMatchStatus);
        Assert.Equal(account.Id, result.AccountId);
    }

    [Fact]
    public void ImportCamt053_MatchesByAStoredMt940AccountId()
    {
        // An account already used for MT940 keeps matching after the bank switches format.
        var account = CreateAccount(
            "UBS Privatkonto",
            bankIdentifier: new AccountBankIdentifier
            {
                Type = AccountBankIdentifierType.Mt940AccountId,
                Value = PrivateIban,
                BankName = "UBS"
            });

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]));

        Assert.Equal(account.Id, Assert.Single(results).AccountId);
    }

    [Fact]
    public void ImportCamt053_MapsEveryFieldOfAPlainStatement()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]));

        var mapping = Assert.Single(results).MappingResult;

        Assert.NotNull(mapping);
        Assert.Equal("CAMT053", mapping.Batch.SourceFormat);
        Assert.Equal(PrivateIban, mapping.Batch.BankAccountIdentifier);
        Assert.Equal("CHF", mapping.Batch.Currency);
        Assert.Equal(new DateOnly(2026, 1, 1), mapping.Batch.OpeningBalanceDate);
        Assert.Equal(new DateOnly(2026, 1, 23), mapping.Batch.ClosingBalanceDate);
        Assert.True(mapping.Batch.ReconciliationBalanced);

        var debit = mapping.Transactions.Single(x => x.BankReference == "9910005GK0615030");

        Assert.Equal(account.Id, debit.AccountId);
        Assert.Equal("CAMT053", debit.SourceFormat);
        Assert.Equal(new DateOnly(2026, 1, 5), debit.ValueDate);
        Assert.Equal(new DateOnly(2026, 1, 5), debit.BookingDate);
        Assert.Equal(-40.00m, debit.SignedAmount);
        Assert.True(debit.IsOutgoing);
        Assert.Equal("CHF", debit.Currency);
        Assert.Equal("PMNT-CCRD-POSD", debit.TransactionCode);
        Assert.Equal("BAZG VIA-WEBSHOP Zahlung UBS TWINT", debit.Description);
        Assert.Null(debit.CustomerReference);
        Assert.Null(debit.Structured86Code);

        var credit = mapping.Transactions.Single(x => x.BankReference == "9999023ZC7856428");

        Assert.Equal(975.75m, credit.SignedAmount);
        Assert.True(credit.IsIncoming);
        Assert.Equal("PMNT-RCDT-ESCT", credit.TransactionCode);
        Assert.Equal("SALARY-2026-01", credit.CustomerReference);

        // The reference TYPE goes into Structured86Code and the reference VALUE into
        // SupplementaryDetails - QRR here, SCOR for an ISO 11649 reference.
        Assert.Equal("QRR", credit.Structured86Code);
        Assert.Equal("210000000003139471430009017", credit.SupplementaryDetails);

        // Raw61/Raw86 are MT940 record names; camt has no such record and the entry XML is not
        // persisted into the localStorage-backed import store.
        Assert.All(mapping.Transactions, x => Assert.Equal(string.Empty, x.Raw61));
        Assert.All(mapping.Transactions, x => Assert.Null(x.Raw86));
    }

    [Fact]
    public void ImportCamt053_MapsIsoCreditorReferences()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.References, [account]));

        var mapping = Assert.Single(results).MappingResult;

        Assert.NotNull(mapping);

        var scor = mapping.Transactions.Single(x => x.BankReference == "REF-SCOR-0001");

        Assert.Equal("SCOR", scor.Structured86Code);
        Assert.Equal("RF18539007547034", scor.SupplementaryDetails);

        // No Ustrd on this entry, so the description falls back to the bank's own entry line,
        // prefixed with the counterparty it does not already name in full.
        Assert.Equal("Swisscom (Schweiz) AG Zahlungsauftrag Swisscom", scor.Description);

        var isr = mapping.Transactions.Single(x => x.BankReference == "REF-ISR-0002");

        Assert.Equal("ISR Reference", isr.Structured86Code);
        Assert.Equal("120000000000234478943216899", isr.SupplementaryDetails);
        Assert.Equal("Krankenkasse Beispiel AG LSV Krankenkasse Februar", isr.Description);

        var unstructured = mapping.Transactions.Single(x => x.BankReference == "REF-USTRD-0003");

        Assert.Null(unstructured.Structured86Code);
        Assert.Null(unstructured.SupplementaryDetails);
        Assert.Equal("Hans Muster Anteil Geschenk Geburtstag Anna", unstructured.Description);
    }

    /// <summary>
    /// The batch-booking guarantee, measured at the level that matters: an internal batch
    /// booking with three <c>TxDtls</c> produces ONE imported transaction carrying the batch
    /// total, and the net amount equals the balance movement.
    /// </summary>
    [Fact]
    public void ImportCamt053_CountsAnInternalBatchBookingOnce()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.BatchBooking, [account]));

        var result = Assert.Single(results);
        var mapping = result.MappingResult;

        Assert.NotNull(mapping);

        // Two entries in, two transactions out - not four, not five.
        Assert.Equal(2, mapping.Transactions.Count);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);

        var batch = mapping.Transactions.Single(x => x.BankReference == "BATCH-2026-01-28-0001");

        Assert.Equal(1500.00m, batch.SignedAmount);

        // Per-item references must not be promoted to the booking: they belong to three
        // different counterparties.
        Assert.Null(batch.CustomerReference);
        Assert.Null(batch.Structured86Code);
        Assert.Null(batch.SupplementaryDetails);

        // The items are still visible to the user, in the description.
        Assert.Contains("Mieter Eins", batch.Description, StringComparison.Ordinal);
        Assert.Contains("Mieter Drei", batch.Description, StringComparison.Ordinal);

        // The net amount equals the balance movement, so nothing was counted twice.
        Assert.Equal(1250.00m, mapping.Batch.TransactionNetAmount);
        Assert.Equal(
            mapping.Batch.ClosingBalance - mapping.Batch.OpeningBalance,
            mapping.Batch.TransactionNetAmount);
        Assert.True(mapping.Batch.ReconciliationBalanced);
    }

    [Fact]
    public void ImportCamt053_ImportsAnEntryWithNoTransactionDetails()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.BatchBooking, [account]));

        var mapping = Assert.Single(results).MappingResult;

        Assert.NotNull(mapping);

        var standingOrder = mapping.Transactions.Single(x => x.BankReference == "SO-2026-01-30-0042");

        Assert.Equal(-250.00m, standingOrder.SignedAmount);
        Assert.Equal("Dauerauftrag Sparen", standingOrder.Description);
        Assert.NotEqual(string.Empty, standingOrder.DeduplicationKey);
    }

    [Fact]
    public void ImportCamt053_ReportsAFailedReconciliation_ForATruncatedFile()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Truncated, [account]));

        var result = Assert.Single(results);

        Assert.True(result.Preview.ReconciliationAvailable);
        Assert.False(result.Preview.ReconciliationBalanced);
        Assert.Equal(975.75m, result.Preview.ReconciliationDifference);

        Assert.NotNull(result.MappingResult);
        Assert.False(result.MappingResult.Batch.ReconciliationBalanced);
        Assert.Equal(975.75m, result.MappingResult.Batch.ReconciliationDifference);
    }

    [Fact]
    public void ImportCamt053_DoesNotAdjustForCharges()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Charges, [account]));

        var mapping = Assert.Single(results).MappingResult;

        Assert.NotNull(mapping);

        var transaction = Assert.Single(mapping.Transactions);

        // 1'005.00 booked = 1'000.00 instructed + 5.00 fee. The fee is already in the entry
        // amount; netting it out again would give -1'000.00 or -1'010.00 and break the balance.
        Assert.Equal(-1005.00m, transaction.SignedAmount);
        Assert.True(mapping.Batch.ReconciliationBalanced);
    }

    [Fact]
    public void ImportCamt053_SkipsEverythingOnASecondImportOfTheSameFile()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);
        var service = new BankStatementImportService();

        var first = Assert.Single(service.ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account])));

        Assert.NotNull(first.MergeResult);
        Assert.Equal(2, first.MergeResult.AddedCount);

        var second = Assert.Single(service.ImportCamt053(
            CreateRequest(
                CamtFixture.Plain08,
                [account],
                first.MergeResult.MergedTransactions)));

        Assert.NotNull(second.MergeResult);
        Assert.Equal(0, second.MergeResult.AddedCount);
        Assert.Equal(2, second.MergeResult.SkippedDuplicateCount);
        Assert.Equal(2, second.MergeResult.MergedTransactions.Count);
    }

    [Fact]
    public void ImportCamt053_DeduplicatesOnTheBankReference()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var mapping = Assert.Single(new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]))).MappingResult;

        Assert.NotNull(mapping);

        // The primary tier is (account, AcctSvcrRef) - the field the Swiss IG names for
        // duplicate checking - not a content hash and not EndToEndId.
        Assert.All(
            mapping.Transactions,
            x => Assert.StartsWith("bank-ref:", x.DeduplicationKey, StringComparison.Ordinal));

        Assert.Equal(
            ImportedBankTransactionDedupKeyBuilder.BuildFromBankReference(
                account.Id,
                "9910005GK0615030"),
            mapping.Transactions.Single(x => x.BankReference == "9910005GK0615030").DeduplicationKey);
    }

    [Fact]
    public void ImportCamt053_ProducesOneBatchPerStatement_ForACombinedFile()
    {
        var privateAccount = CreateAccount("Privatkonto", iban: PrivateIban);
        var savingsAccount = CreateAccount("Sparkonto", iban: SavingsIban);
        var euroAccount = CreateAccount("Eurokonto", iban: EuroIban, currency: "EUR");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(
                CamtFixture.MultiAccount,
                [privateAccount, savingsAccount, euroAccount]));

        Assert.Equal(3, results.Count);
        Assert.All(results, x => Assert.True(x.CanImport));

        Assert.Equal(
            new Guid?[] { privateAccount.Id, savingsAccount.Id, euroAccount.Id },
            results.Select(x => x.AccountId).ToArray());

        // Three distinct batches, never one flattened batch.
        Assert.Equal(
            3,
            results.Select(x => x.MappingResult!.Batch.Id).Distinct().Count());

        Assert.Equal(
            new[] { "CHF", "CHF", "EUR" },
            results.Select(x => x.MappingResult!.Batch.Currency).ToArray());

        // Each statement reconciles against its OWN balances.
        Assert.All(results, x => Assert.True(x.Preview.ReconciliationBalanced));

        Assert.Equal(-250.00m, results[1].MappingResult!.Batch.ClosingBalance);
    }

    [Fact]
    public void ImportCamt053_KeepsEntriesOfOneStatementOutOfAnotherStatementsBatch()
    {
        var privateAccount = CreateAccount("Privatkonto", iban: PrivateIban);
        var savingsAccount = CreateAccount("Sparkonto", iban: SavingsIban);
        var euroAccount = CreateAccount("Eurokonto", iban: EuroIban, currency: "EUR");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(
                CamtFixture.MultiAccount,
                [privateAccount, savingsAccount, euroAccount]));

        foreach (var result in results)
        {
            var mapping = result.MappingResult;

            Assert.NotNull(mapping);

            var transaction = Assert.Single(mapping.Transactions);

            Assert.Equal(mapping.Batch.Id, transaction.ImportBatchId);
            Assert.Equal(mapping.Batch.AccountId, transaction.AccountId);
            Assert.Equal(mapping.Batch.BankAccountIdentifier, transaction.BankAccountIdentifier);
        }

        Assert.Equal(
            new[] { "ACCT-A-0001", "ACCT-B-0001", "ACCT-C-0001" },
            results
                .Select(x => Assert.Single(x.MappingResult!.Transactions).BankReference)
                .ToArray());

        // The final merged list is cumulative across statements: three transactions, one per
        // account, none lost and none duplicated.
        var merged = results[^1].MergeResult!.MergedTransactions;

        Assert.Equal(3, merged.Count);
        Assert.Equal(3, merged.Select(x => x.AccountId).Distinct().Count());
    }

    [Fact]
    public void ImportCamt053_ImportsMatchedStatements_AndFlagsUnmatchedOnes()
    {
        // Two of three accounts are in the plan. Blocking the whole file would make a combined
        // export unusable; instead the third is reported and skipped.
        var privateAccount = CreateAccount("Privatkonto", iban: PrivateIban);
        var euroAccount = CreateAccount("Eurokonto", iban: EuroIban, currency: "EUR");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.MultiAccount, [privateAccount, euroAccount]));

        Assert.Equal(3, results.Count);

        Assert.True(results[0].CanImport);
        Assert.Equal(privateAccount.Id, results[0].AccountId);

        Assert.False(results[1].CanImport);
        Assert.True(results[1].RequiresAccountSelection);
        Assert.Equal(BankStatementAccountMatchStatus.NotMatched, results[1].AccountMatchStatus);
        Assert.Null(results[1].MappingResult);

        // The unmatched statement still reports its IBAN, entry count and reconciliation, so the
        // user can see exactly what was not imported.
        Assert.Equal(SavingsIban, results[1].Preview.BankAccountIdentifier);
        Assert.Equal(SavingsIban, results[1].BankAccountIdentifierToRemember);
        Assert.Equal(1, results[1].Preview.ParsedTransactionCount);
        Assert.True(results[1].Preview.ReconciliationBalanced);

        Assert.True(results[2].CanImport);
        Assert.Equal(euroAccount.Id, results[2].AccountId);
    }

    [Fact]
    public void ImportCamt053_AppliesTheSelectedAccount_WhenExactlyOneStatementIsUnmatched()
    {
        var privateAccount = CreateAccount("Privatkonto", iban: PrivateIban);
        var euroAccount = CreateAccount("Eurokonto", iban: EuroIban, currency: "EUR");
        var newAccount = CreateAccount("Neues Sparkonto");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(
                CamtFixture.MultiAccount,
                [privateAccount, euroAccount, newAccount],
                selectedAccountId: newAccount.Id));

        Assert.Equal(3, results.Count);
        Assert.All(results, x => Assert.True(x.CanImport));

        Assert.Equal(BankStatementAccountMatchStatus.SelectedManually, results[1].AccountMatchStatus);
        Assert.Equal(newAccount.Id, results[1].AccountId);
        Assert.Equal(SavingsIban, results[1].BankAccountIdentifierToRemember);

        // The statements that matched on their own are untouched by the manual selection.
        Assert.Equal(privateAccount.Id, results[0].AccountId);
        Assert.Null(results[0].BankAccountIdentifierToRemember);
    }

    [Fact]
    public void ImportCamt053_IgnoresTheSelectedAccount_WhenSeveralStatementsAreUnmatched()
    {
        // One dropdown cannot express which of two unmatched IBANs the user meant, and guessing
        // would post transactions to the wrong account.
        var privateAccount = CreateAccount("Privatkonto", iban: PrivateIban);
        var otherAccount = CreateAccount("Irgendein Konto");

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(
                CamtFixture.MultiAccount,
                [privateAccount, otherAccount],
                selectedAccountId: otherAccount.Id));

        Assert.Equal(3, results.Count);
        Assert.True(results[0].CanImport);
        Assert.False(results[1].CanImport);
        Assert.False(results[2].CanImport);
    }

    [Fact]
    public void ImportCamt053_TreatsAnAmbiguousIbanMatchAsUnmatched()
    {
        // Two accounts claiming the same IBAN is a data error. Asking beats guessing, and beats
        // throwing an exception the user cannot act on.
        var first = CreateAccount("Konto A", iban: PrivateIban);
        var second = CreateAccount("Konto B", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [first, second]));

        var result = Assert.Single(results);

        Assert.True(result.RequiresAccountSelection);
        Assert.Equal(BankStatementAccountMatchStatus.NotMatched, result.AccountMatchStatus);
    }

    [Fact]
    public void ImportCamt053_SuggestsTheClosingBalance()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var results = new BankStatementImportService().ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account]));

        var suggested = Assert.Single(results).SuggestedBalanceUpdate;

        Assert.NotNull(suggested);
        Assert.Equal(account.Id, suggested.AccountId);
        Assert.Equal(4978.37m, suggested.Balance);
        Assert.Equal("CHF", suggested.Currency);
        Assert.Equal(new DateOnly(2026, 1, 23), suggested.BalanceDate);
        Assert.False(suggested.ClosingBalanceDateLooksSuspicious);
    }

    [Fact]
    public void ImportCamt053_Throws_WhenTheFileIsEmpty()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BankStatementImportService().ImportCamt053(new BankStatementImportRequest
            {
                FileBytes = [],
                FileName = "empty.xml",
                Accounts = [],
                ExistingImportedTransactions = []
            }));

        Assert.Contains("empty", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportCamt053_Throws_WhenTheSelectedAccountDoesNotExist()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            new BankStatementImportService().ImportCamt053(
                CreateRequest(
                    CamtFixture.Plain08,
                    [account],
                    selectedAccountId: Guid.NewGuid())));

        Assert.Contains("Selected account", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_RoutesByContent_NotByFileExtension()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        // A camt file named like an MT940 export still imports as camt.
        var results = new BankStatementImportService().Import(new BankStatementImportRequest
        {
            FileBytes = CamtFixture.ReadBytes(CamtFixture.Plain08),
            FileName = "transactions.mt940",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = AsOfDate
        });

        Assert.Equal("CAMT053", Assert.Single(results).Preview.SourceFormat);
    }

    [Fact]
    public void Import_KeepsTheMt940PathWorking()
    {
        var account = CreateAccount(
            "UBS Privatkonto",
            bankIdentifier: new AccountBankIdentifier
            {
                Type = AccountBankIdentifierType.Mt940AccountId,
                Value = "CH230021021010831140E",
                BankName = "UBS"
            });

        const string Mt940 =
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
            :62F:C260123CHF4978,37
            """;

        // An MT940 export named .xml still imports as MT940.
        var results = new BankStatementImportService().Import(new BankStatementImportRequest
        {
            FileBytes = System.Text.Encoding.UTF8.GetBytes(Mt940),
            FileName = "statement.xml",
            Accounts = [account],
            ExistingImportedTransactions = [],
            AsOfDate = AsOfDate
        });

        var result = Assert.Single(results);

        Assert.Equal("MT940", result.Preview.SourceFormat);
        Assert.Equal(2, result.Preview.ParsedTransactionCount);
        Assert.True(result.Preview.ReconciliationBalanced);
        Assert.Equal(account.Id, result.AccountId);
    }

    [Theory]
    [InlineData(CamtFixture.Plain08, "CAMT053")]
    [InlineData(CamtFixture.Plain04, "CAMT053")]
    [InlineData(CamtFixture.MultiAccount, "CAMT053")]
    public void DetectSourceFormat_RecognisesCamt(string fixtureName, string expected)
    {
        Assert.Equal(
            expected,
            BankStatementImportService.DetectSourceFormat(
                CamtFixture.ReadBytes(fixtureName)));
    }

    [Fact]
    public void Import_SurfacesAnActionableErrorForMalformedXml()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);

        var exception = Assert.Throws<Core.Banking.Camt.Camt053ParseException>(() =>
            new BankStatementImportService().Import(
                CreateRequest(CamtFixture.Malformed, [account])));

        Assert.Contains("not valid XML", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ImportCamt053_ImportsBothSchemaRevisionsIdentically()
    {
        var account = CreateAccount("UBS Privatkonto", iban: PrivateIban);
        var service = new BankStatementImportService();

        var fromV08 = Assert.Single(service.ImportCamt053(
            CreateRequest(CamtFixture.Plain08, [account])));

        var fromV04 = Assert.Single(service.ImportCamt053(
            CreateRequest(CamtFixture.Plain04, [account])));

        Assert.NotNull(fromV08.MappingResult);
        Assert.NotNull(fromV04.MappingResult);

        Assert.Equal(
            fromV08.MappingResult.Transactions
                .Select(x => (x.BankReference, x.ValueDate, x.SignedAmount, x.TransactionCode, x.Description))
                .ToArray(),
            fromV04.MappingResult.Transactions
                .Select(x => (x.BankReference, x.ValueDate, x.SignedAmount, x.TransactionCode, x.Description))
                .ToArray());

        // Same statement, same deduplication keys: importing the .04 export after the .08 one
        // adds nothing.
        var second = Assert.Single(service.ImportCamt053(
            CreateRequest(
                CamtFixture.Plain04,
                [account],
                fromV08.MergeResult!.MergedTransactions)));

        Assert.Equal(0, second.MergeResult!.AddedCount);
        Assert.Equal(2, second.MergeResult.SkippedDuplicateCount);
    }
}
