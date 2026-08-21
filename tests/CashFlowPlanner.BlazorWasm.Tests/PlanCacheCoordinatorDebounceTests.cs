using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Finding P3b: the old autosave returned early while a save was running, discarding that change
/// with no trailing save, and re-serialized the whole plan on every single mutation - including
/// after a simulation run, which does not change the plan at all.
/// </summary>
public sealed class PlanCacheCoordinatorDebounceTests
{
    private static readonly TimeSpan ShortDebounce = TimeSpan.FromMilliseconds(60);

    private static async Task<(PlanCacheCoordinator Coordinator,
        CashFlowAppState State,
        FakeBrowserPlanCache Cache)> CreateInitializedSubjectAsync(TimeSpan debounce)
    {
        var state = new CashFlowAppState();
        var cache = new FakeBrowserPlanCache();

        var coordinator = new PlanCacheCoordinator(
            state,
            new CashFlowPlanJsonSerializer(),
            cache,
            new UiFeedbackService(),
            debounce);

        await coordinator.InitializeAsync();

        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        // Let the initial write settle, including any debounce still in flight, so that tests
        // observe only the writes they cause themselves.
        await coordinator.SaveCurrentPlanAsync();
        await Task.Delay(debounce + TimeSpan.FromMilliseconds(50));
        await coordinator.FlushAsync();

        cache.Writes.Clear();

        return (coordinator, state, cache);
    }

    private static void RenameAccount(CashFlowAppState state, string name)
    {
        var account = state.CurrentPlan!.Accounts
            .Single(x => x.Id == AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdateAccount(new Account
        {
            Id = account.Id,
            Name = name,
            Type = account.Type,
            Currency = account.Currency,
            OpeningBalance = account.OpeningBalance,
            OpeningDate = account.OpeningDate
        });
    }

    private static async Task WaitForWritesAsync(
        FakeBrowserPlanCache cache,
        int expected,
        TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (DateTime.UtcNow < deadline)
        {
            if (cache.Writes.Count >= expected)
            {
                return;
            }

            await Task.Delay(10);
        }
    }

    [Fact]
    public async Task RapidChanges_Should_CollapseIntoOneWrite()
    {
        // Arrange
        var (coordinator, state, cache) = await CreateInitializedSubjectAsync(ShortDebounce);

        // Act - five mutations well inside one debounce window.
        for (var i = 0; i < 5; i++)
        {
            RenameAccount(state, $"Main Account {i}");
        }

        await WaitForWritesAsync(cache, 1, TimeSpan.FromSeconds(5));
        await Task.Delay(ShortDebounce + ShortDebounce);

        // Assert
        Assert.Single(cache.Writes);
        Assert.Contains("Main Account 4", cache.Writes[0]);

        coordinator.Dispose();
    }

    [Fact]
    public async Task ChangeDuringAnInFlightSave_Should_StillBeWritten()
    {
        // Arrange - save immediately, no debounce, so the race is deterministic.
        var (coordinator, state, cache) =
            await CreateInitializedSubjectAsync(TimeSpan.Zero);

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        cache.SaveGate = gate;

        // Act
        RenameAccount(state, "First");

        // A second change arrives while the first write is still blocked. The old boolean guard
        // dropped this one on the floor.
        RenameAccount(state, "Second");

        cache.SaveGate = null;
        gate.SetResult();

        await WaitForWritesAsync(cache, 2, TimeSpan.FromSeconds(5));

        // Assert
        Assert.Equal(2, cache.Writes.Count);
        Assert.Contains("Second", cache.Writes[^1]);

        coordinator.Dispose();
    }

    [Fact]
    public async Task RunSimulation_Should_NotTriggerAWrite()
    {
        // Arrange
        var (coordinator, state, cache) = await CreateInitializedSubjectAsync(TimeSpan.Zero);

        // Act
        state.RunSimulation();

        await Task.Delay(50);

        // Assert - the plan is byte-for-byte identical, so there is nothing to persist.
        Assert.Empty(cache.Writes);

        coordinator.Dispose();
    }

    [Fact]
    public async Task RunSimulation_Should_StillNotifyTheUi()
    {
        // Arrange
        var (coordinator, state, _) = await CreateInitializedSubjectAsync(TimeSpan.Zero);

        var changedCount = 0;
        var simulationChangedCount = 0;
        var planChangedCount = 0;

        state.Changed += () => changedCount++;
        state.SimulationChanged += () => simulationChangedCount++;
        state.PlanChanged += () => planChangedCount++;

        // Act
        state.RunSimulation();

        // Assert
        Assert.Equal(1, changedCount);
        Assert.Equal(1, simulationChangedCount);
        Assert.Equal(0, planChangedCount);

        coordinator.Dispose();
    }

    [Fact]
    public async Task PlanMutation_Should_RaiseBothPlanChangedAndChanged()
    {
        // Arrange
        var (coordinator, state, _) = await CreateInitializedSubjectAsync(TimeSpan.Zero);

        var changedCount = 0;
        var planChangedCount = 0;

        state.Changed += () => changedCount++;
        state.PlanChanged += () => planChangedCount++;

        // Act
        RenameAccount(state, "Renamed");

        // Assert
        Assert.Equal(1, changedCount);
        Assert.Equal(1, planChangedCount);

        coordinator.Dispose();
    }

    [Fact]
    public async Task LastWrite_Should_ReflectTheFinalState()
    {
        // Arrange
        var (coordinator, state, cache) = await CreateInitializedSubjectAsync(ShortDebounce);

        // Act
        RenameAccount(state, "Intermediate");
        RenameAccount(state, "Final");

        await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.NotEmpty(cache.Writes);
        Assert.Contains("Final", cache.Writes[^1]);
        Assert.Contains("Final", (await cache.LoadAsync())!);

        coordinator.Dispose();
    }
}
