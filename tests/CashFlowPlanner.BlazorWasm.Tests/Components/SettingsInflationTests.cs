using Bunit;
using CashFlowPlanner.BlazorWasm.Pages;
using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Storage.Json;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// The plan-level inflation assumption, from the page that sets it.
///
/// Two rules the domain enforces by throwing, in English, from inside validation - and which the
/// user has to be told before the throw: the rate starts at zero and nothing pre-fills it, and a
/// non-zero rate needs the date the plan's amounts are stated in.
/// </summary>
public sealed class SettingsInflationTests : PageTestBase
{
    public SettingsInflationTests()
    {
        // Everything Settings injects. All of it reaches the browser through IJSRuntime, which
        // bUnit answers in loose mode.
        Services.AddSingleton<IWorkingCopyCipher, FakeWorkingCopyCipher>();
        Services.AddSingleton<BrowserPlanCacheService>();
        Services.AddSingleton<BrowserCultureService>();
        Services.AddSingleton<CashFlowPlanJsonSerializer>();
        Services.AddSingleton(new PlanCryptoService(JSInterop.JSRuntime));
        Services.AddSingleton<PassphrasePromptService>();
        Services.AddSingleton<PlanExportPreferences>();
        Services.AddSingleton<PlanFileService>();
        // Registered as a ready-made instance under both of its service types, deliberately.
        // DiskAutoSaveService is IAsyncDisposable-only, and bUnit tears its provider down
        // synchronously, which throws for anything the container itself produced - including
        // through a factory. An instance the container did not create is not its to dispose.
        var disk = new DiskAutoSaveService(JSInterop.JSRuntime);

        Services.AddSingleton(disk);
        Services.AddSingleton<IDiskAutoSave>(disk);
        Services.AddSingleton<DiskAutoSaveCoordinator>();
    }

    private const string SaveInflationLabel = "Save inflation";

    [Fact]
    public void A_fresh_plan_offers_a_rate_of_zero_and_no_base_date()
    {
        // Not 1%, not "the Swiss long-run average". A pre-filled rate would restate every figure
        // in an existing plan the moment its owner opened this page, with nothing to say which
        // numbers moved.
        LoadPlan(AppStateTestPlanFactory.CreatePlan());

        var cut = Render<Settings>();

        Assert.Equal("0.00", FindRateInput(cut).GetAttribute("value"));
        Assert.Equal(0m, AppState.CurrentPlan!.Inflation.AnnualRatePercent);
        Assert.Null(AppState.CurrentPlan.Inflation.BaseDate);
    }

    [Fact]
    public void A_rate_without_a_base_date_is_refused_before_it_reaches_the_plan()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan());

        var cut = Render<Settings>();

        FindRateInput(cut).Change("1.5");
        FindButton(cut, SaveInflationLabel).Click();

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);

        // The plan is untouched rather than half-applied.
        Assert.Equal(0m, AppState.CurrentPlan!.Inflation.AnnualRatePercent);
    }

    [Fact]
    public void A_rate_and_a_base_date_are_written_to_the_plan()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan());

        var cut = Render<Settings>();

        FindRateInput(cut).Change("1.5");
        FindBaseDateInput(cut).Change("2026-01-01");
        FindButton(cut, SaveInflationLabel).Click();

        Assert.Equal(1.5m, AppState.CurrentPlan!.Inflation.AnnualRatePercent);
        Assert.Equal(new DateOnly(2026, 1, 1), AppState.CurrentPlan.Inflation.BaseDate);
        Assert.True(AppState.CurrentPlan.Inflation.IsEnabled);
    }

    [Fact]
    public void Zeroing_the_rate_again_needs_no_base_date_and_turns_inflation_off()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(
            inflation: new InflationAssumption
            {
                AnnualRatePercent = 1.5m,
                BaseDate = new DateOnly(2026, 1, 1)
            }));

        var cut = Render<Settings>();

        FindRateInput(cut).Change("0");
        FindButton(cut, SaveInflationLabel).Click();

        Assert.False(AppState.CurrentPlan!.Inflation.IsEnabled);

        // The date is kept: a user who zeroes the rate while deciding should not have to type the
        // date again when they put the rate back.
        Assert.Equal(new DateOnly(2026, 1, 1), AppState.CurrentPlan.Inflation.BaseDate);
    }

    /// <summary>
    /// The inflation card's rate box. Found through the label rather than by position, so it
    /// survives another card gaining an amount field.
    /// </summary>
    private static AngleSharp.Dom.IElement FindRateInput(IRenderedComponent<Settings> cut) =>
        FindByLabel(cut, "Annual inflation rate");

    private static AngleSharp.Dom.IElement FindBaseDateInput(IRenderedComponent<Settings> cut) =>
        FindByLabel(cut, "Amounts are stated in the money of");

    private static AngleSharp.Dom.IElement FindByLabel(IRenderedComponent<Settings> cut, string label)
    {
        var forId = cut.FindAll("label")
            .Single(x => x.TextContent.Trim().StartsWith(label, StringComparison.Ordinal))
            .GetAttribute("for");

        return cut.Find($"#{forId}");
    }
}
