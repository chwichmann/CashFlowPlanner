namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// The balance-conservation check - <b>only when the file actually carries a balance column</b>.
///
/// <para>
/// This is the honest difference between CSV and camt.053. A camt statement always states an
/// opening and a closing balance, so the check always runs and a truncated download is caught.
/// A CSV export usually states neither. Faking a check there - reporting "balanced" because
/// nothing contradicted us - would turn the one signal that catches a half-downloaded file into
/// a green tick that means nothing. So <see cref="NotAvailable"/> is the normal answer for CSV,
/// and the import says "not available" in the preview rather than "balanced".
/// </para>
///
/// <para>
/// When a running-balance column <i>is</i> present the check is real: the opening balance is
/// the first row's balance minus the first row's amount, the closing balance is the last row's,
/// and the two must differ by the sum of everything in between. Row order is taken from the
/// dates, because plenty of exports are newest-first.
/// </para>
/// </summary>
public sealed class CsvReconciliationResult
{
    public bool IsAvailable { get; init; }

    public bool IsBalanced { get; init; }

    public decimal? OpeningBalance { get; init; }

    public DateOnly? OpeningBalanceDate { get; init; }

    public decimal? ClosingBalance { get; init; }

    public DateOnly? ClosingBalanceDate { get; init; }

    public decimal TransactionNetAmount { get; init; }

    public decimal? ExpectedClosingBalance { get; init; }

    /// <summary>Closing balance minus expected closing balance. Zero when the file balances.</summary>
    public decimal? Difference { get; init; }

    public string Currency { get; init; } = "CHF";

    public static CsvReconciliationResult NotAvailable(
        decimal transactionNetAmount = 0m,
        string currency = "CHF")
    {
        return new CsvReconciliationResult
        {
            IsAvailable = false,
            IsBalanced = false,
            TransactionNetAmount = transactionNetAmount,
            Currency = currency
        };
    }

    public static CsvReconciliationResult Create(
        IReadOnlyList<CsvStatementRow> rows,
        string currency)
    {
        ArgumentNullException.ThrowIfNull(rows);

        var net = rows.Sum(x => x.SignedAmount);

        // Every row must carry a balance. A column that is filled in for some rows and not
        // others cannot support the check, and reporting a difference derived from half the
        // rows would be worse than reporting nothing.
        if (rows.Count < 2 || rows.Any(x => x.Balance is null))
        {
            return NotAvailable(net, currency);
        }

        var ordered = IsDescendingByDate(rows)
            ? rows.Reverse().ToList()
            : rows;

        var first = ordered[0];
        var last = ordered[^1];

        var openingBalance = first.Balance!.Value - first.SignedAmount;
        var closingBalance = last.Balance!.Value;
        var expectedClosingBalance = openingBalance + net;
        var difference = closingBalance - expectedClosingBalance;

        return new CsvReconciliationResult
        {
            IsAvailable = true,
            IsBalanced = difference == 0m,
            OpeningBalance = openingBalance,
            OpeningBalanceDate = first.EffectiveDate,
            ClosingBalance = closingBalance,
            ClosingBalanceDate = last.EffectiveDate,
            TransactionNetAmount = net,
            ExpectedClosingBalance = expectedClosingBalance,
            Difference = difference,
            Currency = currency
        };
    }

    /// <summary>
    /// Newest-first is a common export order and it inverts the running balance. Decided from
    /// the first and last dates rather than by sorting, because rows on the same day must keep
    /// the order the bank gave them - that order is what the running balance follows.
    /// </summary>
    private static bool IsDescendingByDate(IReadOnlyList<CsvStatementRow> rows)
    {
        return rows[0].EffectiveDate > rows[^1].EffectiveDate;
    }
}
