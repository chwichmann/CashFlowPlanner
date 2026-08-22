using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Import;

public static class ImportedBankTransactionDedupKeyBuilder
{
    public static string Build(ImportedBankTransaction transaction)
    {
        return Build(transaction, occurrence: 1);
    }

    /// <summary>
    /// Builds the deduplication key, distinguishing the <paramref name="occurrence"/>-th
    /// identical transaction of a statement from the ones before it.
    ///
    /// <para>
    /// <b>Two coffees on the same afternoon are two transactions.</b> Same date, same 4.50, same
    /// "COOP PRONTO ZUERICH HB" - and the content hash the fallback tier is built from cannot
    /// tell them apart, so without this the second one is silently dropped and the plan is 4.50
    /// short. MT940 and camt.053 do not have the problem because the bank gives every booking an
    /// <c>AcctSvcrRef</c>; CSV usually gives nothing at all, which is what makes the fallback
    /// tier load-bearing rather than a safety net.
    /// </para>
    ///
    /// <para>
    /// The occurrence is the transaction's rank <i>among the identical ones in the same file</i>,
    /// counted in file order - never its row number. That distinction is the whole point: a row
    /// number shifts by one the moment the bank inserts a transaction anywhere above it, and
    /// every key below the insertion would change, so re-importing an overlapping statement
    /// would re-add hundreds of transactions the plan already has. A rank among identical
    /// siblings does not move when unrelated rows appear, so the same file re-imported adds
    /// nothing, a longer export covering the same period adds only what is new, and a genuine
    /// third coffee gets rank 3 and is correctly recognised as new.
    /// </para>
    ///
    /// <para>
    /// Occurrence 1 produces byte-identical output to the single-argument overload, so MT940 and
    /// camt.053 keys - and every key already persisted in a user's browser - are unchanged.
    /// </para>
    /// </summary>
    public static string Build(ImportedBankTransaction transaction, int occurrence)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!string.IsNullOrWhiteSpace(transaction.BankReference))
        {
            // The bank's own booking reference already distinguishes the two coffees. Suffixing
            // it would be wrong, not merely redundant: it would make the key depend on file
            // order for transactions whose identity does not.
            return BuildFromBankReference(
                transaction.AccountId,
                transaction.BankReference);
        }

        var fallback = BuildFallback(transaction);

        return occurrence <= 1
            ? fallback
            : $"{fallback}#{occurrence.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    }

    public static string BuildFromBankReference(
        Guid accountId,
        string bankReference)
    {
        if (string.IsNullOrWhiteSpace(bankReference))
        {
            throw new ArgumentException(
                "Bank reference must not be empty.",
                nameof(bankReference));
        }

        var normalizedBankReference = Normalize(bankReference);

        return $"bank-ref:{accountId:N}:{normalizedBankReference}";
    }

    private static string BuildFallback(ImportedBankTransaction transaction)
    {
        var normalizedDescription = NormalizeText(transaction.Description);

        var rawValue = string.Join(
            "|",
            transaction.AccountId.ToString("N"),
            transaction.ValueDate.ToString("yyyyMMdd"),
            transaction.BookingDate?.ToString("yyyyMMdd") ?? string.Empty,
            transaction.SignedAmount.ToString("0.00"),
            Normalize(transaction.Currency),
            Normalize(transaction.TransactionCode),
            Normalize(transaction.Structured86Code ?? string.Empty),
            normalizedDescription);

        return $"fallback:{CreateSha256(rawValue)}";
    }

    public static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();
    }

    public static string NormalizeText(string value)
    {
        var normalized = Regex.Replace(
            value.Trim(),
            @"\s+",
            " ",
            RegexOptions.CultureInvariant);

        return normalized.ToUpperInvariant();
    }

    private static string CreateSha256(string value)
    {
        var bytes = SHA256.HashData(
            Encoding.UTF8.GetBytes(value));

        return Convert.ToHexString(bytes);
    }
}