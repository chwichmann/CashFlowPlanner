using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Finding P1c: there was no IsDirty, no LastExportedAt and no beforeunload handler anywhere, so
/// closing the tab discarded unexported work in silence.
/// </summary>
public sealed class DirtyTrackingTests
{
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

    [Fact]
    public void FreshlyLoadedPlan_Should_BeClean()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();
        var json = serializer.SerializePlan(AppStateTestPlanFactory.CreatePlan());

        var state = new CashFlowAppState();

        // Act
        state.LoadDocument(serializer.DeserializeDocument(json));

        // Assert
        Assert.False(state.IsDirty);
        Assert.Null(state.LastExportedAt);
    }

    [Fact]
    public void AnyPlanEdit_Should_MarkTheStateDirty()
    {
        // Arrange - start from a loaded plan, which is clean by definition.
        var serializer = new CashFlowPlanJsonSerializer();
        var json = serializer.SerializePlan(AppStateTestPlanFactory.CreatePlan());

        var state = new CashFlowAppState();
        state.LoadDocument(serializer.DeserializeDocument(json));

        var flips = 0;
        state.DirtyStateChanged += () => flips++;

        // Act
        RenameAccount(state, "Renamed");

        // Assert
        Assert.True(state.IsDirty);
        Assert.Equal(1, flips);
    }

    [Fact]
    public void RunSimulation_Should_NotMarkTheStateDirty()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();
        var json = serializer.SerializePlan(AppStateTestPlanFactory.CreatePlan());

        var state = new CashFlowAppState();
        state.LoadDocument(serializer.DeserializeDocument(json));

        // Act
        state.RunSimulation();

        // Assert - the plan did not change, so there is nothing unexported.
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void MarkExported_Should_ClearTheFlagAndRecordTheTime()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();

        var state = new CashFlowAppState();
        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        RenameAccount(state, "Renamed");
        Assert.True(state.IsDirty);

        // Act
        state.MarkExported(serializer.SerializeDocument(state.GetDocumentForSave()));

        // Assert
        Assert.False(state.IsDirty);
        Assert.NotNull(state.LastExportedAt);
    }

    [Fact]
    public void UndoingBackToTheExportedContent_Should_ReportCleanAgain()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();

        var state = new CashFlowAppState();
        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        var exportedJson = serializer.SerializeDocument(state.GetDocumentForSave());
        state.MarkExported(exportedJson);

        RenameAccount(state, "Temporarily Renamed");
        Assert.True(state.IsDirty);

        // Act - the user renames it back, and the next autosave hands the JSON over.
        RenameAccount(state, "Main Account");
        state.NotifyPersistedContent(
            serializer.SerializeDocument(state.GetDocumentForSave()));

        // Assert - this is what the content hash buys over a plain revision counter.
        Assert.False(state.IsDirty);
    }

    [Fact]
    public void PersistedContentThatStillDiffers_Should_StayDirty()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();

        var state = new CashFlowAppState();
        state.SetPlan(AppStateTestPlanFactory.CreatePlan());
        state.MarkExported(serializer.SerializeDocument(state.GetDocumentForSave()));

        // Act
        RenameAccount(state, "Renamed");
        state.NotifyPersistedContent(
            serializer.SerializeDocument(state.GetDocumentForSave()));

        // Assert - caching is not exporting.
        Assert.True(state.IsDirty);
    }

    [Fact]
    public void Clear_Should_ResetDirtyTracking()
    {
        // Arrange
        var state = new CashFlowAppState();
        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        RenameAccount(state, "Renamed");

        // Act
        state.Clear();

        // Assert
        Assert.False(state.IsDirty);
        Assert.Null(state.LastExportedAt);
    }

    [Fact]
    public async Task DirtyState_Should_BePushedToTheUnloadGuard()
    {
        // Arrange
        var state = new CashFlowAppState();
        var guard = new NullUnsavedChangesGuard();

        var coordinator = new PlanCacheCoordinator(
            state,
            new CashFlowPlanJsonSerializer(),
            new FakeBrowserPlanCache(),
            new UiFeedbackService(),
            TimeSpan.Zero,
            guard);

        await coordinator.InitializeAsync();

        Assert.False(guard.LastValue);

        // Act
        state.SetPlan(AppStateTestPlanFactory.CreatePlan());

        // Assert
        Assert.True(guard.LastValue);

        coordinator.Dispose();
    }
}
