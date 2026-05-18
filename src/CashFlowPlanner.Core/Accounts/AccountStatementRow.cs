namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountStatementRow
{
    public DateOnly ValutaDate { get; init; }

    public required string Title { get; init; }

    public decimal? Incoming { get; init; }

    public decimal? Outgoing { get; init; }

    public decimal Balance { get; init; }

    public string Currency { get; init; } = "CHF";

    public string? Category { get; init; }

    public string? Counterparty { get; init; }

    public string? Notes { get; init; }

    public Guid SourceEventId { get; init; }
}