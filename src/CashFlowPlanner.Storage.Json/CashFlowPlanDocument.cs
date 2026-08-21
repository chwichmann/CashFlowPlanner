using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Pillar3a;
using CashFlowPlanner.Core.RealEstate;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CashFlowPlanner.Storage.Json;

public sealed class CashFlowPlanDocument
{
    public int SchemaVersion { get; set; } = CashFlowPlanJsonSerializer.CurrentSchemaVersion;

    public Guid PlanId { get; init; }

    public string Name { get; init; } = string.Empty;

    public string BaseCurrency { get; init; } = "CHF";

    public Guid? DefaultPaymentAccountId { get; init; }

    public bool TreatWeekendsAsBankOffDays { get; init; } = true;

    public List<BankOffDay> BankOffDays { get; init; } = [];

    public List<Person> Persons { get; init; } = [];

    public List<Account> Accounts { get; init; } = [];

    public List<TransactionDefinition> Transactions { get; init; } = [];

    public List<MortgageContract> Mortgages { get; init; } = [];

    public List<CreditCardContract> CreditCards { get; init; } = [];

    public List<Pillar3aContract> Pillar3aContracts { get; init; } = [];

    public List<HouseBuySimulatorScenario> HouseBuyScenarios { get; init; } = [];

    public SimulationSettings SimulationSettings { get; init; } = new();

    /// <summary>
    /// Every top-level JSON property that has no matching document property is captured here on
    /// load and written back verbatim on save. Without this bucket a load/save round trip silently
    /// deletes fields this build does not know about yet (finding P2a: the shipped sample lost
    /// <c>createdAt</c>, <c>modifiedAt</c> and <c>notes</c>).
    /// </summary>
    [JsonExtensionData]
    public Dictionary<string, JsonElement>? ExtensionData { get; set; }
}