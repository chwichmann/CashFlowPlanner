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

    private void EnsureSameCurrency(Money other)
    {
        if (!string.Equals(Currency, other.Currency, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Currency mismatch: {Currency} != {other.Currency}");
        }
    }

    public override string ToString()
        => $"{Amount:N2} {Currency}";
}