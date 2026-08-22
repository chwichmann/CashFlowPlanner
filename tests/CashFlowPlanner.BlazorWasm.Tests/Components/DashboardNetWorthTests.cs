using Bunit;
using CashFlowPlanner.BlazorWasm.Pages;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// Net worth, and the real/nominal toggle.
///
/// The dashboard used to compute its own net worth - accounts, minus mortgage principal - beside a
/// chart that summed active account balances and called that net worth too. Two implementations,
/// neither of which could see the household's property, so on a plan with a house both were wrong
/// by the largest number in the plan and could disagree with each other as well.
/// </summary>
public sealed class DashboardNetWorthTests : PageTestBase
{
    private static CashFlowPlan PlanWithHouse(InflationAssumption? inflation = null)
    {
        var mortgage = AppStateTestPlanFactory.CreateMortgage(AppStateTestPlanFactory.MainAccountId);

        return AppStateTestPlanFactory.CreatePlan(
            mortgages: [mortgage],
            realEstateAssets:
            [
                AppStateTestPlanFactory.CreateRealEstateAsset(linkedMortgageIds: [mortgage.Id])
            ],
            inflation: inflation);
    }

    private IRenderedComponent<Dashboard> RenderDashboard(CashFlowPlan plan)
    {
        LoadPlan(plan);
        AppState.RunSimulation();

        return Render<Dashboard>();
    }

    private static IReadOnlyList<string> BalanceSheetLabels(IRenderedComponent<Dashboard> cut) =>
        cut.FindAll("table tbody tr td:first-child")
            .Select(x => x.TextContent.Trim())
            .ToList();

    [Fact]
    public void The_balance_sheet_shows_the_property_the_household_owns()
    {
        var cut = RenderDashboard(PlanWithHouse());

        Assert.Contains("Real estate", BalanceSheetLabels(cut), StringComparer.Ordinal);

        var point = AppState.CurrentSimulationResult!.TryGetNetWorth(
            AppState.CurrentPlan!.SimulationSettings.EndDate);

        Assert.NotNull(point);
        Assert.Equal(950_000m, point.RealEstateValue);

        // And the page agrees with the series rather than being a second computation of it.
        Assert.Contains(
            Digits(point.NetWorth),
            BalanceSheetAmounts(cut),
            StringComparer.Ordinal);
    }

    [Fact]
    public void The_components_sum_exactly_to_the_total()
    {
        // The whole point of storing them separately: a wrong total traces to one component
        // rather than to the arithmetic.
        var cut = RenderDashboard(PlanWithHouse());

        var point = AppState.CurrentSimulationResult!.TryGetNetWorth(
            AppState.CurrentPlan!.SimulationSettings.EndDate)!;

        Assert.Equal(
            point.LiquidAssets + point.InvestmentAssets + point.Pillar3aAssets + point.RealEstateValue,
            point.TotalAssets);

        Assert.Equal(point.TotalAssets - point.TotalLiabilities, point.NetWorth);

        Assert.Contains("Total assets", BalanceSheetLabels(cut), StringComparer.Ordinal);
    }

    [Fact]
    public void The_toggle_is_offered_even_with_no_inflation_and_then_does_nothing()
    {
        // A control that only appears the day a rate is entered is a control nobody finds. With no
        // rate the two readings are identical, so showing it costs nothing.
        var cut = RenderDashboard(PlanWithHouse());

        // The domain says it outright: with no assumption the real series is the nominal list
        // itself, not a copy of it.
        var result = AppState.CurrentSimulationResult!;

        Assert.Same(
            result.GetNetWorthPoints(AmountBasis.Nominal),
            result.GetNetWorthPoints(AmountBasis.Real));

        var nominal = BalanceSheetAmounts(cut);

        FindButton(cut, "Real (today's money)").Click();

        Assert.Equal(nominal, BalanceSheetAmounts(cut));
    }

    [Fact]
    public void Switching_to_real_deflates_the_figures_and_says_so_on_the_axis()
    {
        var cut = RenderDashboard(PlanWithHouse(new InflationAssumption
        {
            AnnualRatePercent = 10m,
            BaseDate = new DateOnly(2020, 1, 1)
        }));

        // A chart that switches basis without saying so is worse than one that never offered the
        // choice, so the axis label carries the basis and, in real terms, the date.
        Assert.Contains("francs of the day", cut.Markup, StringComparison.Ordinal);

        FindButton(cut, "Real (today's money)").Click();

        Assert.Contains("in money of", cut.Markup, StringComparison.Ordinal);

        var nominalPoint = AppState.CurrentSimulationResult!
            .GetNetWorthPoints(AmountBasis.Nominal)[^1];

        var realPoint = AppState.CurrentSimulationResult
            .GetNetWorthPoints(AmountBasis.Real)[^1];

        // Six completed years at 10% is a factor well clear of any rounding argument.
        Assert.True(realPoint.NetWorth < nominalPoint.NetWorth);
        Assert.Equal(realPoint.Date, nominalPoint.Date);
    }

    [Fact]
    public void The_page_says_what_the_figures_do_not_contain()
    {
        // TAX-MODEL.md section 8, and it obliges this not to be a footnote: a household reading a
        // long-horizon net-worth figure that silently omits decades of tax is being misled by the
        // omission, and the omission is invisible unless the app says so.
        var cut = RenderDashboard(PlanWithHouse());

        var text = cut.Markup;

        Assert.Contains("Taxes are not modelled", text, StringComparison.Ordinal);
        Assert.Contains("Pillar 2", text, StringComparison.Ordinal);
        Assert.Contains("AHV", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// The amount column of the balance sheet, reduced to its digits.
    ///
    /// The column rather than the whole markup, so the assertion is about the figures and not
    /// about which toggle button is highlighted - and the digits rather than the rendered text,
    /// because <see cref="System.Globalization.CultureInfo.CurrentCulture"/> is ambient and the
    /// culture-formatting tests in this assembly move it on the thread pool underneath a render
    /// that spans two of them. 950'000.00 and 950.000,00 are the same figure, and this test is
    /// about the figure.
    /// </summary>
    private static IReadOnlyList<string> BalanceSheetAmounts(IRenderedComponent<Dashboard> cut) =>
        cut.FindAll("table tbody tr td:nth-child(2)")
            .Select(x => new string(x.TextContent.Where(char.IsDigit).ToArray()))
            .ToList();

    private static string Digits(decimal value) =>
        new(value.ToString("N2", System.Globalization.CultureInfo.InvariantCulture)
            .Where(char.IsDigit)
            .ToArray());
}
