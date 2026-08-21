namespace CashFlowPlanner.Core;

public sealed record Money
{
    public decimal Amount { get; init; }

    public string Currency { get; init; } = "CHF";

    public Money()
    {
    }

    public Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    public static Money Zero(string currency = "CHF")
        => new(0m, currency);

    public Money Add(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount + other.Amount };
    }

    public Money Subtract(Money other)
    {
        EnsureSameCurrency(other);
        return this with { Amount = Amount - other.Amount };
    }

    public Money Negate()
        => this with { Amount = -Amount };

    /// <summary>
    /// The single rule for deciding whether two currency codes are the same one.
    /// Case-insensitive, and a missing code counts as "unspecified" and therefore
    /// matches anything rather than raising a false alarm.
    ///
    /// Lives here so the guard inside this type and the plan-level and
    /// posting-level checks in the engine cannot drift apart. The rest of the
    /// domain still carries plain decimals plus a currency string; migrating it
    /// to <see cref="Money"/> is a separate, much larger change.
    /// </summary>
    public static bool IsSameCurrency(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right))
        {
            return true;
        }

        return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private void EnsureSameCurrency(Money other)
    {
        if (!IsSameCurrency(Currency, other.Currency))
        {
            throw new InvalidOperationException(
                $"Currency mismatch: {Currency} != {other.Currency}");
        }
    }

    public override string ToString()
        => $"{Amount:N2} {Currency}";
}