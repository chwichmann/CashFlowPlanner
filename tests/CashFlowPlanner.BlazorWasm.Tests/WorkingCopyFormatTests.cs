using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Quality-of-life items: the working copy is written compact to save quota, and the previous good
/// copy is kept under a .prev key as one-step recovery from a bad save.
/// </summary>
public sealed class WorkingCopyFormatTests
{
    private static async Task<(PlanCacheCoordinator Coordinator,
        CashFlowAppState State,
        FakeBrowserPlanCache Cache)> CreateSubjectAsync()
    {
        var state = new CashFlowAppState();
        var cache = new FakeBrowserPlanCache();

        var coordinator = new PlanCacheCoordinator(
            state,
            new CashFlowPlanJsonSerializer(),
            cache,
            new UiFeedbackService(),
            TimeSpan.Zero);

        await coordinator.InitializeAsync();

        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        await coordinator.SaveCurrentPlanAsync();

        return (coordinator, state, cache);
    }

    [Fact]
    public async Task WorkingCopy_Should_BeWrittenCompact()
    {
        // Arrange / Act
        var (coordinator, _, cache) = await CreateSubjectAsync();

        // Assert
        Assert.NotEmpty(cache.Writes);
        Assert.DoesNotContain("\n", cache.Writes[^1]);

        coordinator.Dispose();
    }

    [Fact]
    public async Task WorkingCopy_Should_StillBeRestorable()
    {
        // Arrange
        var (coordinator, _, cache) = await CreateSubjectAsync();

        // Act - a fresh session restoring from the same cache.
        var restoredState = new CashFlowAppState();

        var restoringCoordinator = new PlanCacheCoordinator(
            restoredState,
            new CashFlowPlanJsonSerializer(),
            cache,
            new UiFeedbackService(),
            TimeSpan.Zero);

        await restoringCoordinator.InitializeAsync();

        // Assert
        Assert.NotNull(restoredState.CurrentPlan);
        Assert.Equal("Test Plan", restoredState.CurrentPlan!.Name);

        coordinator.Dispose();
        restoringCoordinator.Dispose();
    }

    [Fact]
    public async Task PreviousGoodCopy_Should_BeKeptForRecovery()
    {
        // Arrange
        var (coordinator, state, cache) = await CreateSubjectAsync();

        var firstCopy = await cache.LoadAsync();

        // Act
        var account = state.CurrentPlan!.Accounts
            .Single(x => x.Id == AppStateTestPlanFactory.MainAccountId);

        state.AddOrUpdateAccount(new Account
        {
            Id = account.Id,
            Name = "Renamed Account",
            Type = account.Type,
            Currency = account.Currency,
            OpeningBalance = account.OpeningBalance,
            OpeningDate = account.OpeningDate
        });

        await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Contains("Renamed Account", (await cache.LoadAsync())!);
        Assert.Equal(firstCopy, await cache.LoadPreviousAsync());

        coordinator.Dispose();
    }
}
