using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.BlazorWasm.Services.BankImport;
using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// The imported-statement store holds the same class of private data as the plan itself -
/// counterparties, amounts, IBANs, references - so it gets the same treatment as the working
/// copy. These tests exist because it did not: it was written straight to
/// <c>cashflowplanner.bankimport.v1</c> as readable JSON while the plan beside it was encrypted.
/// </summary>
public sealed class BankImportStoreEncryptionTests
{
    private const string Key = "cashflowplanner.bankimport.v1";

    private static readonly Guid AccountId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static ImportedBankTransactionMergeResult Merge(string description)
    {
        var batch = new ImportedBankStatementBatch
        {
            AccountId = AccountId,
            SourceFormat = "CAMT.053",
            FileName = "statement.xml",
            FileFingerprint = "fingerprint",
            BankAccountIdentifier = "CH9300762011623852957"
        };

        var transaction = new ImportedBankTransaction
        {
            ImportBatchId = batch.Id,
            AccountId = AccountId,
            SourceFormat = "CAMT.053",
            BankAccountIdentifier = "CH9300762011623852957",
            ValueDate = new DateOnly(2026, 3, 14),
            SignedAmount = -1234.55m,
            Description = description,
            DeduplicationKey = description
        };

        return new ImportedBankTransactionMergeResult
        {
            Batch = batch,
            MergedTransactions = [transaction],
            AddedTransactions = [transaction]
        };
    }

    private static (BankImportStoreLocalStorage Store, FakeLocalStorageJsRuntime Js, FakeWorkingCopyCipher Cipher) Create()
    {
        var js = new FakeLocalStorageJsRuntime();
        var cipher = new FakeWorkingCopyCipher();

        return (new BankImportStoreLocalStorage(js, cipher), js, cipher);
    }

    [Fact]
    public async Task AnImport_IsWritten_AsAnEnvelope()
    {
        var (store, js, _) = Create();

        await store.InitializeAsync();
        await store.ApplyImportAsync(Merge("MIETE WOHNUNG ZUERICH"));

        var stored = js.Items[Key];

        Assert.StartsWith(WorkingCopyEnvelope.Prefix, stored);

        // The point of the exercise: the counterparty and the IBAN must not be greppable in the
        // browser profile.
        Assert.DoesNotContain("MIETE WOHNUNG ZUERICH", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("CH9300762011623852957", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("1234.55", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AnEncryptedImport_RoundTrips()
    {
        var (store, js, cipher) = Create();

        await store.InitializeAsync();
        await store.ApplyImportAsync(Merge("MIETE WOHNUNG ZUERICH"));

        var reopened = new BankImportStoreLocalStorage(js, cipher);

        await reopened.InitializeAsync();

        var transactions = await reopened.GetTransactionsForAccountAsync(AccountId);

        Assert.Equal("MIETE WOHNUNG ZUERICH", Assert.Single(transactions).Description);
    }

    [Fact]
    public async Task PlaintextFromAnOlderBuild_IsRead_ThenRewrittenEncrypted()
    {
        var (seed, js, cipher) = Create();

        // Produce a real state document, then put it back as the plaintext an older build wrote.
        await seed.InitializeAsync();
        await seed.ApplyImportAsync(Merge("ALTE ZAHLUNG"));

        js.Items[Key] = await cipher.UnprotectAsync(js.Items[Key]) ?? string.Empty;

        Assert.DoesNotContain(WorkingCopyEnvelope.Prefix, js.Items[Key], StringComparison.Ordinal);

        var migrating = new BankImportStoreLocalStorage(js, cipher);

        await migrating.InitializeAsync();

        // Nobody loses a reconciled import to the format change...
        var transactions = await migrating.GetTransactionsForAccountAsync(AccountId);

        Assert.Equal("ALTE ZAHLUNG", Assert.Single(transactions).Description);

        // ...and the next write leaves it encrypted.
        await migrating.ApplyImportAsync(Merge("NEUE ZAHLUNG"));

        Assert.StartsWith(WorkingCopyEnvelope.Prefix, js.Items[Key]);
    }

    [Fact]
    public async Task AnUnopenableEnvelope_StartsEmpty_RatherThanThrowing()
    {
        var (seed, js, cipher) = Create();

        await seed.InitializeAsync();
        await seed.ApplyImportAsync(Merge("MIETE"));

        // The device key was cleared with site data. The imports are gone, but the app must
        // still start - the statement files are re-importable, an unhandled exception is not.
        cipher.DeviceKeyLost = true;

        var reopened = new BankImportStoreLocalStorage(js, cipher);

        await reopened.InitializeAsync();

        Assert.Empty(await reopened.GetAllTransactionsAsync());
    }
}
