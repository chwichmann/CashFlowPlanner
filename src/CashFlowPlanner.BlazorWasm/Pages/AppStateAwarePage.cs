using CashFlowPlanner.BlazorWasm.Services;
using Microsoft.AspNetCore.Components;

namespace CashFlowPlanner.BlazorWasm.Pages;

/// <summary>
/// Base for pages that render plan or simulation state, so they re-render when that state is
/// changed from somewhere else.
/// <para>
/// This exists because of what removing <c>@key</c> from <c>MainLayout</c> exposed (finding U5).
/// That key was bumped on every one of the ~28 state mutations, which threw away the whole page
/// subtree and built a new page instance - taking filter text, the selected tab, scroll position,
/// focus and any half-typed edit with it. Removing it fixed that, but revealed that the pages had
/// been relying on being destroyed and rebuilt: none of them subscribe to anything.
/// </para>
/// <para>
/// Calling <c>StateHasChanged</c> on the layout is not enough, and it is worth writing down why.
/// <c>@Body</c> renders the routed page as a <em>parameterless</em> child component. Blazor's
/// differ sees the same component type with no changed parameters, retains the existing instance
/// and skips <c>SetParametersAsync</c> entirely - so the page never learns anything happened. The
/// visible symptom was pressing the run button in the navigation bar while on the Dashboard: the
/// banner said the simulation had completed while the page still said none had been run.
/// </para>
/// <para>
/// Pages that override <see cref="OnInitialized"/> must call <c>base.OnInitialized()</c>, and
/// pages that need their own cleanup must override <see cref="Dispose"/> and call
/// <c>base.Dispose()</c>.
/// </para>
/// </summary>
public abstract class AppStateAwarePage : ComponentBase, IDisposable
{
    // Deliberately not named AppState: every page already declares its own
    // `@inject CashFlowAppState AppState`, and a member of the same name on the base would
    // collide. Both resolve to the same singleton.
    [Inject]
    private CashFlowAppState AppStateForRefresh { get; set; } = default!;

    protected override void OnInitialized()
    {
        // `Changed` covers both plan and simulation changes; the narrower PlanChanged and
        // SimulationChanged events exist for persistence, which must not fire on a simulation run.
        AppStateForRefresh.Changed += OnAppStateChangedRefresh;
    }

    private void OnAppStateChangedRefresh()
    {
        InvokeAsync(StateHasChanged);
    }

    public virtual void Dispose()
    {
        AppStateForRefresh.Changed -= OnAppStateChangedRefresh;

        GC.SuppressFinalize(this);
    }
}
