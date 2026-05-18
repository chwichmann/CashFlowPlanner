namespace CashFlowPlanner.Core.Mortgages;

public sealed class MortgagePrincipalPoint
{
    public DateOnly Date { get; init; }

    public Guid MortgageId { get; init; }

    public required string MortgageName { get; init; }

    /// <summary>
    /// Positive outstanding principal.
    /// Example: 705000 CHF.
    /// </summary>
    public decimal Principal { get; init; }

    public string Currency { get; init; } = "CHF";
}