using System.Globalization;
using Bunit;
using CashFlowPlanner.BlazorWasm.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// Shared setup for the shared component library's tests.
///
/// The Blazor project had 12,900 lines and not one component test, which is how 122 labels ended
/// up without a <c>for</c>, 28 tables without a responsive wrapper and a totals row a column short
/// of its header - none of those are things a compiler can catch, and nothing else was looking.
/// </summary>
public abstract class ComponentTestBase : BunitContext
{
    protected ComponentTestBase()
    {
        // Only what the shared components inject. Anything heavier (PlanEmptyState's file and
        // crypto services) is stubbed per test rather than registered globally, so a test failure
        // points at the component under test and not at its scenery.
        Services.AddLocalization();
        Services.AddSingleton<AppFormatter>();
        Services.AddSingleton<CashFlowAppState>();

        // Components call ElementReference.FocusAsync and import a collocated JS module; in a test
        // there is no browser to answer. Loose mode records the calls and returns defaults, which
        // is exactly what the focus assertions then inspect.
        JSInterop.Mode = JSRuntimeMode.Loose;
    }

    /// <summary>
    /// Runs <paramref name="action"/> with a specific formatting culture and puts the old one back.
    ///
    /// Culture is ambient state on the thread, so a test that sets it and walks away breaks the
    /// next test in the same collection - and the failure lands somewhere else entirely.
    /// </summary>
    protected static void WithCulture(string cultureName, Action action)
    {
        var previous = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(cultureName);
            action();
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
