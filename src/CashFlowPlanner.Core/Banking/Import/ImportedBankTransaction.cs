namespace CashFlowPlanner.Core.Banking.Import;

public sealed class ImportedBankTransaction
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public Guid ImportBatchId { get; init; }

    public Guid AccountId { get; init; }

    public string SourceFormat { get; init; } = "MT940";

    public string BankAccountIdentifier { get; init; } = string.Empty;

    public DateOnly ValueDate { get; init; }

    public DateOnly? BookingDate { get; init; }

    public decimal SignedAmount { get; init; }

    public decimal Amount =>
        Math.Abs(SignedAmount);

    public bool IsIncoming =>
        SignedAmount > 0m;

    public bool IsOutgoing =>
        SignedAmount < 0m;

    public string Currency { get; init; } = "CHF";

    public string TransactionCode { get; init; } = string.Empty;

    public string? Structured86Code { get; init; }

    public string? BankReference { get; init; }

    public string? CustomerReference { get; init; }

    public string? SupplementaryDetails { get; init; }

    public string Description { get; init; } = string.Empty;

    public string Raw61 { get; init; } = string.Empty;

    public string? Raw86 { get; init; }

    public string DeduplicationKey { get; init; } = string.Empty;

    public Guid? MatchedTransactionDefinitionId { get; set; }

    public ImportedTransactionMatchStatus MatchStatus { get; set; } =
        ImportedTransactionMatchStatus.Unmatched;

    public DateTime ImportedUtc { get; init; } = DateTime.UtcNow;

    public override string ToString()
    {
        return $"{ValueDate:yyyy-MM-dd}: {SignedAmount:N2} {Currency} {Description}";
    }
}