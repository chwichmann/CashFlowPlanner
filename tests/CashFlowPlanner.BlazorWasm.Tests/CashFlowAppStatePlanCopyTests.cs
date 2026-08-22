using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Finding P2a, in a third place.
///
/// Every mutation on <see cref="CashFlowAppState"/> rebuilds the plan by naming each property
/// explicitly. <c>RealEstateAssets</c> and <c>Inflation</c> arrived on <see cref="CashFlowPlan"/>
/// after those fourteen constructions were written, so until this test existed, editing a person -
/// or deleting any account at all - silently discarded the household's property valuation and the
/// plan's inflation assumption. Nothing threw: the next export simply had less in it.
///
/// One test per mutation, because the failure mode is silence rather than an exception.
/// </summary>
public sealed class CashFlowAppStatePlanCopyTests
{
    private static readonly InflationAssumption Inflation = new()
    {
        AnnualRatePercent = 1.2m,
        BaseDate = new DateOnly(2026, 1, 1)
    };

    private static CashFlowAppState CreateState(out RealEstateAsset asset)
    {
        asset = AppStateTestPlanFactory.CreateRealEstateAsset();

        var plan = AppStateTestPlanFactory.CreatePlan(
            realEstateAssets: [asset],
            inflation: Inflation);

        var state = new CashFlowAppState();
        state.SetPlan(plan);

        return state;
    }

    private static void AssertPreserved(CashFlowAppState state)
    {
        var plan = state.CurrentPlan!;

        Assert.Single(plan.RealEstateAssets);
        Assert.Equal("Family Home", plan.RealEstateAssets[0].Name);
        Assert.Equal(950_000m, plan.RealEstateAssets[0].CurrentEstimatedValue);

        Assert.Equal(1.2m, plan.Inflation.AnnualRatePercent);
        Assert.Equal(new DateOnly(2026, 1, 1), plan.Inflation.BaseDate);

        // And they have to survive the trip through the document too, or the export drops what
        // the in-memory plan still has.
        var document = state.GetDocumentForSave();

        Assert.Single(document.RealEstateAssets);
        Assert.Equal(1.2m, document.Inflation.AnnualRatePercent);
    }

    [Fact]
    public void DeleteAccount_Preserves_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        state.DeleteAccount(AppStateTestPlanFactory.SpareAccountId);

        AssertPreserved(state);
    }

    [Fact]
    public void UpdateSimulationSettings_Preserves_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        state.UpdateSimulationSettings(new SimulationSettings
        {
            DateMode = SimulationDateMode.ExplicitDateRange,
            StartDate = new DateOnly(2027, 1, 1),
            EndDate = new DateOnly(2028, 1, 1)
        });

        AssertPreserved(state);
    }

    [Fact]
    public void UpdatePlanDefaultsAndBankCalendar_Preserves_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        state.UpdatePlanDefaultsAndBankCalendar(
            AppStateTestPlanFactory.MainAccountId,
            treatWeekendsAsBankOffDays: false,
            bankOffDays: []);

        AssertPreserved(state);
    }

    [Fact]
    public void UpdatePlanName_Preserves_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        state.UpdatePlanName("Renamed");

        AssertPreserved(state);
    }

    [Fact]
    public void AddOrUpdateMortgage_And_Delete_Preserve_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        var mortgage = AppStateTestPlanFactory.CreateMortgage(
            AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdateMortgage(mortgage);
        AssertPreserved(state);

        state.DeleteMortgage(mortgage.Id);
        AssertPreserved(state);
    }

    [Fact]
    public void AddOrUpdateCreditCard_And_Delete_Preserve_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        var creditCard = AppStateTestPlanFactory.CreateCreditCard(
            AppStateTestPlanFactory.CardAccountId,
            AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdateCreditCard(creditCard);
        AssertPreserved(state);

        state.DeleteCreditCard(creditCard.Id);
        AssertPreserved(state);
    }

    [Fact]
    public void AddOrUpdatePerson_And_Delete_Preserve_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Second Person"
        };

        state.AddOrUpdatePerson(person);
        AssertPreserved(state);

        state.DeletePerson(person.Id);
        AssertPreserved(state);
    }

    [Fact]
    public void AddOrUpdatePillar3aContract_And_Delete_Preserve_RealEstateAndInflation()
    {
        var state = CreateState(out _);

        var contract = AppStateTestPlanFactory.CreatePillar3aContract(
            contributionAccountId: AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdatePillar3aContract(contract);
        AssertPreserved(state);

        state.DeletePillar3aContract(contract.Id);
        AssertPreserved(state);
    }

    [Fact]
    public void AddOrUpdateRealEstateAsset_Replaces_By_Id()
    {
        var state = CreateState(out var asset);

        state.AddOrUpdateRealEstateAsset(new RealEstateAsset
        {
            Id = asset.Id,
            Name = "Family Home",
            Type = RealEstateType.Flat,
            CurrentEstimatedValue = 1_000_000m
        });

        Assert.Single(state.CurrentPlan!.RealEstateAssets);
        Assert.Equal(1_000_000m, state.CurrentPlan.RealEstateAssets[0].CurrentEstimatedValue);
        Assert.Equal(RealEstateType.Flat, state.CurrentPlan.RealEstateAssets[0].Type);
    }

    [Fact]
    public void AddOrUpdateRealEstateAsset_Rejects_A_Mortgage_Linked_Twice()
    {
        var state = CreateState(out _);

        var mortgage = AppStateTestPlanFactory.CreateMortgage(
            AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdateMortgage(mortgage);

        state.AddOrUpdateRealEstateAsset(
            AppStateTestPlanFactory.CreateRealEstateAsset(
                id: new Guid("20000000-0000-0000-0000-00000000000a"),
                linkedMortgageIds: [mortgage.Id]));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            state.AddOrUpdateRealEstateAsset(
                AppStateTestPlanFactory.CreateRealEstateAsset(
                    id: new Guid("20000000-0000-0000-0000-00000000000b"),
                    linkedMortgageIds: [mortgage.Id])));

        Assert.Contains("more than one real estate asset", exception.Message, StringComparison.Ordinal);

        // Rejected means rejected: the plan still holds only the first asset plus the fixture one.
        Assert.Equal(2, state.CurrentPlan!.RealEstateAssets.Count);
    }

    [Fact]
    public void DeleteRealEstateAsset_Removes_It()
    {
        var state = CreateState(out var asset);

        state.DeleteRealEstateAsset(asset.Id);

        Assert.Empty(state.CurrentPlan!.RealEstateAssets);
        Assert.Equal(1.2m, state.CurrentPlan.Inflation.AnnualRatePercent);
    }

    [Fact]
    public void UpdateInflation_Rejects_A_Rate_Without_A_Base_Date()
    {
        var state = CreateState(out _);

        Assert.Throws<InvalidOperationException>(() =>
            state.UpdateInflation(new InflationAssumption { AnnualRatePercent = 2m }));

        // The old assumption stands rather than being half-applied.
        Assert.Equal(1.2m, state.CurrentPlan!.Inflation.AnnualRatePercent);
    }

    [Fact]
    public void UpdateInflation_Back_To_Zero_Needs_No_Base_Date()
    {
        var state = CreateState(out _);

        state.UpdateInflation(new InflationAssumption());

        Assert.Equal(0m, state.CurrentPlan!.Inflation.AnnualRatePercent);
        Assert.Null(state.CurrentPlan.Inflation.BaseDate);
        Assert.Single(state.CurrentPlan.RealEstateAssets);
    }

    [Fact]
    public void DeleteAccount_Refuses_An_Account_A_Pillar3a_Contract_Is_Linked_To()
    {
        // The link is what makes a contribution a transfer rather than money leaving the plan
        // (finding H8). Deleting the account behind it left the contract pointing at nothing.
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    accountId: AppStateTestPlanFactory.Pillar3aAccountId)
            ],
            withPillar3aAccount: true);

        var state = new CashFlowAppState();
        state.SetPlan(plan);

        var exception = Assert.Throws<InvalidOperationException>(
            () => state.DeleteAccount(AppStateTestPlanFactory.Pillar3aAccountId));

        Assert.Contains("contract account", exception.Message, StringComparison.Ordinal);
        Assert.Equal(4, state.CurrentPlan!.Accounts.Count);
    }
}
