namespace CashFlowPlanner.Core;

public static class CashFlowPlanFactory
{
    public static CashFlowPlan CreateEmpty(
        string name = "Private Cashflow",
        string baseCurrency = "CHF")
    {
        var today = DateOnly.FromDateTime(DateTime.Today);

        return new CashFlowPlan
        {
            Name = name,
            BaseCurrency = baseCurrency,
            Accounts = [],
            Transactions = [],
            Mortgages = [],
            CreditCards = [],
            Persons = [],
            SimulationSettings = new SimulationSettings
            {
                StartDate = today,
                EndDate = today.AddYears(1)
            }
        };
    }
}