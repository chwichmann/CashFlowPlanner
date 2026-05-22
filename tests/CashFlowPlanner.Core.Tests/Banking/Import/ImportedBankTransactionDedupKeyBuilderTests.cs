using CashFlowPlanner.Core.Banking.Import;

namespace CashFlowPlanner.Core.Tests.Banking.Import;

public sealed class ImportedBankTransactionDedupKeyBuilderTests
{
    [Fact]
    public void Build_UsesBankReference_WhenAvailable()
    {
        var accountId = Guid.NewGuid();

        var transaction = new ImportedBankTransaction
        {
            AccountId = accountId,
            ValueDate = new DateOnly(2026, 1, 5),
            SignedAmount = -40m,
            Currency = "CHF",
            TransactionCode = "NMSC",
            BankReference = "9910005GK0615030",
            Description = "Payment"
        };

        var key = ImportedBankTransactionDedupKeyBuilder.Build(transaction);

        Assert.Equal(
            $"bank-ref:{accountId:N}:9910005GK0615030",
            key);
    }

    [Fact]
    public void Build_NormalizesBankReference()
    {
        var accountId = Guid.NewGuid();

        var transaction = new ImportedBankTransaction
        {
            AccountId = accountId,
            ValueDate = new DateOnly(2026, 1, 5),
            SignedAmount = -40m,
            Currency = "CHF",
            TransactionCode = "NMSC",
            BankReference = " 9910 005-gk0615030 ",
            Description = "Payment"
        };

        var key = ImportedBankTransactionDedupKeyBuilder.Build(transaction);

        Assert.Equal(
            $"bank-ref:{accountId:N}:9910005GK0615030",
            key);
    }

    [Fact]
    public void Build_UsesStableFallback_WhenNoBankReferenceExists()
    {
        var accountId = Guid.NewGuid();

        var transaction = new ImportedBankTransaction
        {
            AccountId = accountId,
            ValueDate = new DateOnly(2026, 1, 5),
            BookingDate = new DateOnly(2026, 1, 5),
            SignedAmount = -40m,
            Currency = "CHF",
            TransactionCode = "NMSC",
            Structured86Code = "K70",
            Description = "BAZG VIA-WEBSHOP Zahlung UBS TWINT"
        };

        var key1 = ImportedBankTransactionDedupKeyBuilder.Build(transaction);
        var key2 = ImportedBankTransactionDedupKeyBuilder.Build(transaction);

        Assert.StartsWith("fallback:", key1);
        Assert.Equal(key1, key2);
    }

    [Fact]
    public void Build_FallbackNormalizesDescriptionWhitespace()
    {
        var accountId = Guid.NewGuid();

        var transaction1 = new ImportedBankTransaction
        {
            AccountId = accountId,
            ValueDate = new DateOnly(2026, 1, 5),
            BookingDate = new DateOnly(2026, 1, 5),
            SignedAmount = -40m,
            Currency = "CHF",
            TransactionCode = "NMSC",
            Structured86Code = "K70",
            Description = "BAZG VIA-WEBSHOP Zahlung UBS TWINT"
        };

        var transaction2 = new ImportedBankTransaction
        {
            AccountId = accountId,
            ValueDate = new DateOnly(2026, 1, 5),
            BookingDate = new DateOnly(2026, 1, 5),
            SignedAmount = -40m,
            Currency = "CHF",
            TransactionCode = "NMSC",
            Structured86Code = "K70",
            Description = " BAZG   VIA-WEBSHOP     Zahlung UBS TWINT "
        };

        var key1 = ImportedBankTransactionDedupKeyBuilder.Build(transaction1);
        var key2 = ImportedBankTransactionDedupKeyBuilder.Build(transaction2);

        Assert.Equal(key1, key2);
    }
}