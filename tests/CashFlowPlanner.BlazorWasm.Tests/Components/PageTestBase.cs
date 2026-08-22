using AngleSharp.Dom;
using Bunit;
using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Pillar3a;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// Shared setup for tests that render a whole routable page rather than one shared component.
///
/// A page is where the interesting mistakes live - a picker offering the wrong accounts, an editor
/// that drops a collection on save - and none of those are visible from the outside of the
/// components it is built from.
/// </summary>
public abstract class PageTestBase : ComponentTestBase
{
    protected PageTestBase()
    {
        // Exactly what Program.cs registers for these pages, and nothing more.
        Services.AddSingleton<EnumLocalizer>();
        Services.AddSingleton<Pillar3aProjectionEngine>();
        Services.AddSingleton<Pillar3aTaxYearSimulator>();
        Services.AddSingleton<UiFeedbackService>();
        Services.AddSingleton<DashboardSummaryService>();
        Services.AddSingleton<MonthlyCashflowSummaryService>();
    }

    protected CashFlowAppState AppState => Services.GetRequiredService<CashFlowAppState>();

    protected void LoadPlan(CashFlowPlan plan) => AppState.SetPlan(plan);

    /// <summary>
    /// The one button whose visible text is <paramref name="text"/>.
    ///
    /// bUnit's selectors are CSS, and CSS cannot match on text; every alternative - an id, a
    /// data-testid, an nth-child index - either adds markup that exists only for the test or
    /// breaks the moment a button moves. The label is what the user clicks, so it is what the test
    /// clicks.
    /// </summary>
    protected static IElement FindButton(
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut,
        string text)
    {
        return FindButton(cut, text, containerSelector: null);
    }

    /// <summary>
    /// As above, restricted to one region of the page. A dialog and the editor behind it both have
    /// a "Save"; which one the test means is a real question and not one to answer by index.
    /// </summary>
    protected static IElement FindButton(
        IRenderedComponent<Microsoft.AspNetCore.Components.IComponent> cut,
        string text,
        string? containerSelector)
    {
        var buttons = containerSelector is null
            ? cut.FindAll("button")
            : cut.FindAll(containerSelector).SelectMany(x => x.QuerySelectorAll("button")).ToList();

        var matches = buttons
            .Where(x => string.Equals(x.TextContent.Trim(), text, StringComparison.Ordinal))
            .ToList();

        Assert.True(
            matches.Count == 1,
            $"Expected exactly one button labelled '{text}'"
            + (containerSelector is null ? string.Empty : $" inside '{containerSelector}'")
            + $", found {matches.Count}.");

        return matches[0];
    }
}
