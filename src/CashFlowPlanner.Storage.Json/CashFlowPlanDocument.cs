using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Storage.Json;

public sealed class CashFlowPlanDocument
{
    public int SchemaVersion { get; init; } = 1;

    public Guid PlanId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string BaseCurrency { get; init; } = "CHF";

    public List<Person> Persons { get; init; } = [];

    public List<Account> Accounts { get; init; } = [];

    public List<TransactionDefinition> Transactions { get; init; } = [];

    public List<MortgageContract> Mortgages { get; init; } = [];

    public List<CreditCardContract> CreditCards { get; init; } = [];

    // ✅ NEW
    public List<Pillar3aContract> Pillar3aContracts { get; init; } = [];

    public List<HouseBuySimulatorScenario> HouseBuyScenarios { get; init; } = [];

    public SimulationSettings SimulationSettings { get; init; } = new();
}