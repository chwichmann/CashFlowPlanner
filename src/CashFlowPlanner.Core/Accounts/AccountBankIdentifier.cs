namespace CashFlowPlanner.Core.Accounts;

public sealed class AccountBankIdentifier
{
    public AccountBankIdentifierType Type { get; init; }

    public required string Value { get; init; }

    public string? BankName { get; init; }

    public string? Notes { get; init; }

    public string NormalizedValue =>
        Normalize(Value);

    public static string Normalize(string value)
    {
        return value
            .Trim()
            .Replace(" ", string.Empty)
            .Replace("-", string.Empty)
            .ToUpperInvariant();
    }

    public override string ToString()
    {
        return string.IsNullOrWhiteSpace(BankName)
            ? $"{Type}: {Value}"
            : $"{BankName} {Type}: {Value}";
    }
}