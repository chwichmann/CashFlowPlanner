using System.Globalization;
using System.Runtime.CompilerServices;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Pins the culture for the whole test assembly, so a run on a developer's machine matches a
/// run in CI.
/// <para>
/// The component tests find controls by their English label - <c>FindButton(cut, "Save")</c> -
/// because that is what the neutral resource file says. Culture is ambient, so on a machine
/// whose locale is German the localizer answered from <c>SharedResource.de.resx</c> instead and
/// thirty-six of them failed, for a reason that has nothing to do with what they were testing.
/// The CI runner has no such locale, so this was invisible there and only ever bit the person
/// whose UI language the app is actually written in.
/// </para>
/// <para>
/// Formatting is pinned to the invariant culture for the same reason: an assertion on rendered
/// text that does not say which culture it expects otherwise passes or fails by geography.
/// A test that cares about a specific culture wraps the render in
/// <c>ComponentTestBase.WithCulture</c>, which sets and restores it around that one block.
/// </para>
/// </summary>
internal static class TestCulture
{
    [ModuleInitializer]
    internal static void Pin()
    {
        // Both the default and the current, and for UI as well as formatting: bUnit renders
        // through a dispatcher that may hop threads, and a fresh thread reads the
        // DefaultThreadCurrent* pair rather than whatever this thread was set to.
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.CurrentUICulture = CultureInfo.InvariantCulture;
    }
}
