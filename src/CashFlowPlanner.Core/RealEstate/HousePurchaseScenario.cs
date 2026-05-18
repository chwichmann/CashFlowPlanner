namespace CashFlowPlanner.Core.RealEstate;

public sealed class HousePurchaseScenario
{
    public decimal BuyPrice { get; init; }

    public decimal RenovationPrice { get; init; }

    public decimal DesiredMortgage { get; init; }

    public List<EquitySource> EquitySources { get; init; } = [];

    public List<PersonIncome> Incomes { get; init; } = [];

    public SwissMortgageRuleSettings Rules { get; init; } = new();

    public void Validate()
    {
        if (BuyPrice <= 0)
            throw new InvalidOperationException("Buy price must be > 0.");

        if (DesiredMortgage < 0)
            throw new InvalidOperationException("Mortgage must not be negative.");
    }
}