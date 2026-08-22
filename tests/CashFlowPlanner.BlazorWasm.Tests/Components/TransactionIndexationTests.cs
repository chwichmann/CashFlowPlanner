using AngleSharp.Dom;
using Bunit;
using CashFlowPlanner.BlazorWasm.Pages;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// The per-transaction half of inflation.
///
/// A single plan-wide rate applied to everything would be as wrong as no rate at all: rent and
/// groceries track prices, a fixed-rate mortgage instalment does not, and a salary follows its own
/// path. The editor has to be able to say which, and it has to not lose the answer.
/// </summary>
public sealed class TransactionIndexationTests : PageTestBase
{
    private static readonly Guid TransactionId = new("50000000-0000-0000-0000-000000000001");

    private static TransactionDefinition Salary(
        IndexationMode mode = IndexationMode.PlanDefault,
        decimal? rate = null,
        DateOnly? baseDate = null) => new()
        {
            Id = TransactionId,
            Name = "Salary",
            Kind = TransactionKind.ExternalIncome,
            ToAccountId = AppStateTestPlanFactory.MainAccountId,
            Amount = 8000m,
            Currency = "CHF",
            IndexationMode = mode,
            AnnualIndexationRatePercent = rate,
            IndexationBaseDate = baseDate,
            Schedule = new Schedule
            {
                Frequency = ScheduleFrequency.Monthly,
                StartDate = new DateOnly(2026, 1, 1),
                DayOfMonth = 25
            }
        };

    private IRenderedComponent<Transactions> RenderWithSalary(TransactionDefinition salary)
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(
            transactions: [salary],
            inflation: new InflationAssumption
            {
                AnnualRatePercent = 1m,
                BaseDate = new DateOnly(2026, 1, 1)
            }));

        var cut = Render<Transactions>();

        OpenRecurringEditor(cut);

        return cut;
    }

    /// <summary>
    /// The salary recurs monthly, so it lives on the recurring tab. The tab is found by position
    /// within the tab strip rather than by label: its label carries a count badge, so the visible
    /// text changes with the fixture.
    /// </summary>
    private static void OpenRecurringEditor(IRenderedComponent<Transactions> cut)
    {
        cut.FindAll("ul.nav-tabs button")[1].Click();
        FindButton(cut, "Edit").Click();
    }

    private static IElement ModeSelect(IRenderedComponent<Transactions> cut) =>
        cut.Find("details.transaction-indexation select");

    private static TransactionDefinition Saved(CashFlowPlan plan) =>
        plan.Transactions.Single(x => x.Id == TransactionId);

    [Fact]
    public void The_three_fields_are_behind_a_disclosure_and_default_to_following_the_plan()
    {
        var cut = RenderWithSalary(Salary());

        // <details> rather than a Bootstrap collapse: no JavaScript, keyboard-operable and
        // announced without any aria of its own.
        var disclosure = cut.Find("details.transaction-indexation");

        Assert.False(disclosure.HasAttribute("open"));

        Assert.Equal(
            IndexationMode.PlanDefault.ToString(),
            ModeSelect(cut).GetAttribute("value"));

        // The rate box only exists for the mode that uses it.
        Assert.Empty(cut.FindAll("details.transaction-indexation input[inputmode='decimal']"));
    }

    [Fact]
    public void The_rate_appears_only_for_its_own_mode()
    {
        var cut = RenderWithSalary(Salary());

        Assert.Empty(cut.FindAll("details.transaction-indexation input[inputmode='decimal']"));

        ModeSelect(cut).Change(IndexationMode.Custom.ToString());

        Assert.Single(cut.FindAll("details.transaction-indexation input[inputmode='decimal']"));
    }

    [Fact]
    public void A_custom_mode_without_a_rate_is_refused_and_the_plan_is_untouched()
    {
        // TransactionDefinition.Validate rejects this too, in English and from inside the domain.
        // Said here it names the field the user has to go back and fill in.
        var cut = RenderWithSalary(Salary());

        ModeSelect(cut).Change(IndexationMode.Custom.ToString());
        FindButton(cut, "Save").Click();

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);
        Assert.Equal(IndexationMode.PlanDefault, Saved(AppState.CurrentPlan!).IndexationMode);
    }

    [Fact]
    public void A_salary_with_its_own_rate_is_written_to_the_plan()
    {
        // Salary progression needs no feature of its own: an income with its own rate is exactly
        // that.
        var cut = RenderWithSalary(Salary());

        ModeSelect(cut).Change(IndexationMode.Custom.ToString());
        cut.Find("details.transaction-indexation input[inputmode='decimal']").Change("2.5");
        cut.Find("details.transaction-indexation input[type='date']").Change("2024-07-01");

        FindButton(cut, "Save").Click();

        var saved = Saved(AppState.CurrentPlan!);

        Assert.Equal(IndexationMode.Custom, saved.IndexationMode);
        Assert.Equal(2.5m, saved.AnnualIndexationRatePercent);
        Assert.Equal(new DateOnly(2024, 7, 1), saved.IndexationBaseDate);
    }

    [Fact]
    public void Opening_and_saving_a_transaction_keeps_the_indexation_it_already_had()
    {
        // The regression that costs the most and shows the least: an editor that reads a field but
        // does not write it back turns every unrelated edit into a silent reset.
        var cut = RenderWithSalary(Salary(
            IndexationMode.Custom,
            rate: 2.5m,
            baseDate: new DateOnly(2024, 7, 1)));

        FindButton(cut, "Save").Click();

        var saved = Saved(AppState.CurrentPlan!);

        Assert.Equal(IndexationMode.Custom, saved.IndexationMode);
        Assert.Equal(2.5m, saved.AnnualIndexationRatePercent);
        Assert.Equal(new DateOnly(2024, 7, 1), saved.IndexationBaseDate);
    }

    [Fact]
    public void Switching_back_to_the_plans_rate_drops_the_custom_one()
    {
        // A rate left behind by a mode the user has since changed would sit in the file looking
        // meaningful, and be ignored by the engine.
        var cut = RenderWithSalary(Salary(
            IndexationMode.Custom,
            rate: 2.5m,
            baseDate: new DateOnly(2024, 7, 1)));

        ModeSelect(cut).Change(IndexationMode.PlanDefault.ToString());
        FindButton(cut, "Save").Click();

        var saved = Saved(AppState.CurrentPlan!);

        Assert.Equal(IndexationMode.PlanDefault, saved.IndexationMode);
        Assert.Null(saved.AnnualIndexationRatePercent);
    }

    [Fact]
    public void A_plan_with_no_inflation_rate_says_that_following_it_does_nothing()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(transactions: [Salary()]));

        var cut = Render<Transactions>();

        OpenRecurringEditor(cut);

        // Otherwise "follow plan" reads as a setting that is doing something when it is not.
        Assert.Contains(
            "The plan's inflation rate is zero",
            cut.Find("details.transaction-indexation").TextContent,
            StringComparison.Ordinal);
    }
}
