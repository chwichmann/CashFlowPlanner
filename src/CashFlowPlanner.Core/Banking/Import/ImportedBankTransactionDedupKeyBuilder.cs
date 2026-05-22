using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace CashFlowPlanner.Core.Banking.Import;

public static class ImportedBankTransactionDedupKeyBuilder
{
    public static string Build(ImportedBankTransaction transaction)
    {
        ArgumentNullException.ThrowIfNull(transaction);

        if (!string.IsNullOrWhiteSpace(transaction.BankReference))
        {
            return BuildFromBankReference(
                transaction.AccountId,
                transaction.BankReference);
        }

        return BuildFallback(transaction);
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