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
            Id = Guid.NewGuid(),
            Name = name,
            BaseCurrency = baseCurrency,

            DefaultPaymentAccountId = null,
            TreatWeekendsAsBankOffDays = true,
            BankOffDays = [],

            Accounts = [],
            Transactions = [],
            Mortgages = [],
            CreditCards = [],
            Persons = [],
            Pillar3aContracts = [],
            HouseBuyScenarios = [],

            SimulationSettings = new SimulationSettings
            {
                StartDate = today,
                EndDate = today.AddYears(1)
            }
        };
    }
}