using CashFlowPlanner.Core;
using System.Text.Json;

namespace CashFlowPlanner.Storage.Json;

public static class CashFlowPlanDocumentMapper
{
    public static CashFlowPlan ToPlan(
        this CashFlowPlanDocument document)
    {
        return new CashFlowPlan
        {
            Id = document.PlanId,
            Name = document.Name,
            BaseCurrency = document.BaseCurrency,

            DefaultPaymentAccountId = document.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = document.TreatWeekendsAsBankOffDays,
            BankOffDays = document.BankOffDays ?? [],

            Persons = document.Persons ?? [],
            Accounts = document.Accounts ?? [],
            Transactions = document.Transactions ?? [],
            Mortgages = document.Mortgages ?? [],
            CreditCards = document.CreditCards ?? [],
            Pillar3aContracts = document.Pillar3aContracts ?? [],
            HouseBuyScenarios = document.HouseBuyScenarios ?? [],
            SimulationSettings = document.SimulationSettings ?? new()
        };
    }

    public static CashFlowPlanDocument ToDocument(
        this CashFlowPlan plan,
        CashFlowPlanDocument? existing = null)
    {
        return new CashFlowPlanDocument
        {
            SchemaVersion = CashFlowPlanJsonSerializer.CurrentSchemaVersion,

            // Unknown fields captured on load are carried forward, otherwise every save would
            // delete them (finding P2a).
            ExtensionData = existing?.ExtensionData is null
                ? null
                : new Dictionary<string, JsonElement>(existing.ExtensionData),

            PlanId = plan.Id,
            Name = plan.Name,
            BaseCurrency = plan.BaseCurrency,

            DefaultPaymentAccountId = plan.DefaultPaymentAccountId,
            TreatWeekendsAsBankOffDays = plan.TreatWeekendsAsBankOffDays,
            BankOffDays = plan.BankOffDays,

            Persons = plan.Persons,
            Accounts = plan.Accounts,
            Transactions = plan.Transactions,
            Mortgages = plan.Mortgages,
            CreditCards = plan.CreditCards,
            Pillar3aContracts = plan.Pillar3aContracts,
            HouseBuyScenarios = plan.HouseBuyScenarios,
            SimulationSettings = plan.SimulationSettings
        };
    }
}