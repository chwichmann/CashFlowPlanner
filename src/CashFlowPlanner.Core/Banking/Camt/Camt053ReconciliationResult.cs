namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// The balance-conservation check, asserted on every import: <c>CLBD - OPBD == sum of signed
/// Ntry amounts</c>, evaluated <b>per statement</b> and never across the file.
///
/// <para>
/// One check catches the three ways a camt import goes wrong: a truncated or partially
/// downloaded file, batch bookings summed at the wrong level (entry *and* detail), and charges
/// added to or subtracted from booked amounts they are already part of. It mirrors what
/// <c>Mt940ReconciliationResult</c> does for the MT940 path.
/// </para>
/// </summary>
public sealed class Camt053ReconciliationResult
{
    public bool IsAvailable { get; init; }

    public bool IsBalanced { get; init; }

    public decimal? OpeningBalance { get; init; }

    public decimal? ClosingBalance { get; init; }

    public decimal EntryNetAmount { get; init; }

    public decimal? ExpectedClosingBalance { get; init; }

    /// <summary>Closing balance minus expected closing balance. Zero when the statement balances.</summary>
    public decimal? Difference { get; init; }

    public string Currency { get; init; } = "CHF";

    public static Camt053ReconciliationResult NotAvailable()
    {
        return new Camt053ReconciliationResult
        {
            IsAvailable = false,
            IsBalanced = false
        };
    }

    public static Camt053ReconciliationResult Create(
        Camt053Balance? openingBalance,
        Camt053Balance? closingBalance,
        IReadOnlyCollection<Camt053Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        // Sum at entry level only. Details are enrichment, never addends.
        var entryNetAmount = entries.Sum(x => x.SignedAmount);

        if (openingBalance is null || closingBalance is null)
        {
            return new Camt053ReconciliationResult
            {
                IsAvailable = false,
                IsBalanced = false,
                OpeningBalance = openingBalance?.SignedAmount,
                ClosingBalance = closingBalance?.SignedAmount,
                EntryNetAmount = entryNetAmount,
                Currency =
                    closingBalance?.Currency
                    ?? openingBalance?.Currency
                    ?? "CHF"
            };
        }

        var expectedClosingBalance = openingBalance.SignedAmount + entryNetAmount;
        var difference = closingBalance.SignedAmount - expectedClosingBalance;

        return new Camt053ReconciliationResult
        {
            IsAvailable = true,
            IsBalanced = difference == 0m,
            OpeningBalance = openingBalance.SignedAmount,
            ClosingBalance = closingBalance.SignedAmount,
            EntryNetAmount = entryNetAmount,
            ExpectedClosingBalance = expectedClosingBalance,
            Difference = difference,
            Currency = closingBalance.Currency
        };
    }
}
