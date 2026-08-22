using AngleSharp.Dom;
using Bunit;
using CashFlowPlanner.BlazorWasm.Pages;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// The real-estate editor.
///
/// <see cref="CashFlowPlan.Validate"/> enforces every rule here by throwing, from three levels
/// down and in English, at the moment the user presses a button that is supposed to work. These
/// tests are about the page saying the same thing first, in the user's language and naming the
/// field to go back to - and about the defaults it does not fill in.
/// </summary>
public sealed class RealEstatePageTests : PageTestBase
{
    private static IElement FieldByLabel(IRenderedComponent<RealEstate> cut, string label)
    {
        var forId = cut.FindAll("label")
            .Single(x => x.TextContent.Trim().StartsWith(label, StringComparison.Ordinal))
            .GetAttribute("for");

        return cut.Find($"#{forId}");
    }

    private IRenderedComponent<RealEstate> RenderNewAssetEditor(CashFlowPlan plan)
    {
        LoadPlan(plan);

        var cut = Render<RealEstate>();

        FindButton(cut, "Add property").Click();

        return cut;
    }

    [Fact]
    public void A_new_property_pre_fills_no_growth_rate_and_no_dates()
    {
        // Zero growth holds the property flat, which is the only assumption-free default and the
        // behaviour every plan had before this collection existed.
        var cut = RenderNewAssetEditor(AppStateTestPlanFactory.CreatePlan());

        Assert.Equal("0.00", FieldByLabel(cut, "Assumed annual growth").GetAttribute("value"));
        Assert.Equal(string.Empty, FieldByLabel(cut, "Valued on").GetAttribute("value") ?? string.Empty);
        Assert.Equal(string.Empty, FieldByLabel(cut, "Owned since").GetAttribute("value") ?? string.Empty);
        Assert.Equal(string.Empty, FieldByLabel(cut, "Sold on").GetAttribute("value") ?? string.Empty);
    }

    [Fact]
    public void A_property_with_a_name_and_a_value_is_saved()
    {
        var cut = RenderNewAssetEditor(AppStateTestPlanFactory.CreatePlan());

        FieldByLabel(cut, "Name").Change("Family Home");
        FieldByLabel(cut, "Estimated value").Change("950000");

        FindButton(cut, "Save").Click();

        var asset = Assert.Single(AppState.CurrentPlan!.RealEstateAssets);

        Assert.Equal("Family Home", asset.Name);
        Assert.Equal(950_000m, asset.CurrentEstimatedValue);
        Assert.Equal(0m, asset.AnnualValueGrowthPercent);
        Assert.Null(asset.AcquisitionDate);

        // With no dates, the property counts for the whole horizon - which is what the list says.
        Assert.True(asset.IsOwnedOn(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void A_growth_rate_without_a_valuation_date_is_refused_before_the_save()
    {
        var cut = RenderNewAssetEditor(AppStateTestPlanFactory.CreatePlan());

        FieldByLabel(cut, "Name").Change("Family Home");
        FieldByLabel(cut, "Estimated value").Change("950000");
        FieldByLabel(cut, "Assumed annual growth").Change("1.5");

        FindButton(cut, "Save").Click();

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(AppState.CurrentPlan!.RealEstateAssets);
    }

    [Fact]
    public void A_sale_on_or_before_the_purchase_is_refused()
    {
        // Added to the domain after the handoff was written, along with the dates themselves.
        var cut = RenderNewAssetEditor(AppStateTestPlanFactory.CreatePlan());

        FieldByLabel(cut, "Name").Change("Family Home");
        FieldByLabel(cut, "Estimated value").Change("950000");
        FieldByLabel(cut, "Owned since").Change("2030-01-01");
        FieldByLabel(cut, "Sold on").Change("2030-01-01");

        FindButton(cut, "Save").Click();

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(AppState.CurrentPlan!.RealEstateAssets);
    }

    [Fact]
    public void A_negative_value_is_refused()
    {
        var cut = RenderNewAssetEditor(AppStateTestPlanFactory.CreatePlan());

        FieldByLabel(cut, "Name").Change("Family Home");
        FieldByLabel(cut, "Estimated value").Change("-1");

        FindButton(cut, "Save").Click();

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);
        Assert.Empty(AppState.CurrentPlan!.RealEstateAssets);
    }

    [Fact]
    public void A_mortgage_another_property_already_claims_cannot_be_ticked()
    {
        // Linking one mortgage to two properties nets the same debt off two different assets.
        // Plan validation rejects it; the editor refuses the click, which is better than refusing
        // the save.
        var mortgage = AppStateTestPlanFactory.CreateMortgage(AppStateTestPlanFactory.MainAccountId);

        var plan = AppStateTestPlanFactory.CreatePlan(
            mortgages: [mortgage],
            realEstateAssets:
            [
                AppStateTestPlanFactory.CreateRealEstateAsset(linkedMortgageIds: [mortgage.Id])
            ]);

        var cut = RenderNewAssetEditor(plan);

        var checkbox = cut.Find(".realestate-mortgages input[type='checkbox']");

        Assert.True(checkbox.HasAttribute("disabled"));
        Assert.Contains("Already linked to", cut.Find(".realestate-mortgages").TextContent, StringComparison.Ordinal);
    }

    [Fact]
    public void A_free_mortgage_can_be_linked_and_is_written_to_the_plan()
    {
        var mortgage = AppStateTestPlanFactory.CreateMortgage(AppStateTestPlanFactory.MainAccountId);

        var cut = RenderNewAssetEditor(
            AppStateTestPlanFactory.CreatePlan(mortgages: [mortgage]));

        FieldByLabel(cut, "Name").Change("Family Home");
        FieldByLabel(cut, "Estimated value").Change("950000");
        cut.Find(".realestate-mortgages input[type='checkbox']").Change(true);

        FindButton(cut, "Save").Click();

        var asset = Assert.Single(AppState.CurrentPlan!.RealEstateAssets);

        Assert.Equal(mortgage.Id, Assert.Single(asset.LinkedMortgageIds));
    }

    [Fact]
    public void A_purchase_date_that_does_not_match_the_mortgage_start_is_pointed_out()
    {
        // A mortgage counts as debt from the day it starts. A purchase whose two legs fall on
        // different days makes the net-worth series step twice - once for the property and once
        // for the debt - which reads as a windfall followed by a loss.
        var mortgage = AppStateTestPlanFactory.CreateMortgage(AppStateTestPlanFactory.MainAccountId);

        var cut = RenderNewAssetEditor(
            AppStateTestPlanFactory.CreatePlan(mortgages: [mortgage]));

        cut.Find(".realestate-mortgages input[type='checkbox']").Change(true);
        FieldByLabel(cut, "Owned since").Change("2030-06-01");

        Assert.Contains("alert-warning", cut.Markup, StringComparison.Ordinal);

        // Advisory, not a refusal: the domain permits it and there are plans where it is right.
        FieldByLabel(cut, "Name").Change("Family Home");
        FindButton(cut, "Save").Click();

        Assert.Single(AppState.CurrentPlan!.RealEstateAssets);
    }

    [Fact]
    public void Editing_a_property_keeps_everything_it_already_had()
    {
        var plan = AppStateTestPlanFactory.CreatePlan(
            realEstateAssets: [AppStateTestPlanFactory.CreateRealEstateAsset()]);

        LoadPlan(plan);

        var cut = Render<RealEstate>();

        FindButton(cut, "Edit").Click();
        FindButton(cut, "Save").Click();

        var asset = Assert.Single(AppState.CurrentPlan!.RealEstateAssets);

        Assert.Equal(950_000m, asset.CurrentEstimatedValue);
        Assert.Equal(1m, asset.AnnualValueGrowthPercent);
        Assert.Equal(new DateOnly(2026, 1, 1), asset.ValuationDate);
        Assert.Equal(50_000m, asset.Pillar2BvgUsedAmount);
    }

    [Fact]
    public void The_list_says_a_property_with_no_dates_is_owned_throughout()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(
            realEstateAssets:
            [
                new RealEstateAsset
                {
                    Name = "Family Home",
                    CurrentEstimatedValue = 950_000m
                }
            ]));

        var cut = Render<RealEstate>();

        Assert.Contains("Whole horizon", cut.Markup, StringComparison.Ordinal);
    }
}
