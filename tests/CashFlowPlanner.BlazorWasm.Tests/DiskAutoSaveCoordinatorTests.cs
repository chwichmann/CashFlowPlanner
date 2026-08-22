using CashFlowPlanner.BlazorWasm.Resources;
using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Storage.Json;
using Microsoft.Extensions.Localization;
using Microsoft.JSInterop;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// The "export needed" badge is the app's data-loss indicator, so it has to stay believable.
/// It was reported from a real session: with a file linked and being written successfully, the
/// badge still said an export was needed, because only the manual export path cleared it.
/// Nagging about a file that was just written teaches people to ignore the one warning that
/// matters.
/// </summary>
public sealed class DiskAutoSaveCoordinatorTests
{
    private sealed class FakeDisk : IDiskAutoSave
    {
        public event Action? StatusChanged;

        public DiskLinkStatus Status { get; set; } =
            new(DiskLinkState.Granted, "plan.json");

        public List<string> Writes { get; } = [];

        public DiskWriteResult NextResult { get; set; } =
            new(true, "written", null, "plan.json");

        public Task<DiskWriteResult> WriteAsync(string text)
        {
            Writes.Add(text);

            if (!NextResult.Ok && NextResult.NeedsPermission)
            {
                Status = Status with { State = DiskLinkState.NeedsPermission };
                StatusChanged?.Invoke();
            }

            return Task.FromResult(NextResult);
        }
    }

    private sealed class NullLocalizer : IStringLocalizer<SharedResource>
    {
        public LocalizedString this[string name] => new(name, name, resourceNotFound: false);

        public LocalizedString this[string name, params object[] arguments] =>
            new(name, string.Format(name, arguments), resourceNotFound: false);

        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures) => [];
    }

    private static (DiskAutoSaveCoordinator Coordinator, CashFlowAppState State, FakeDisk Disk)
        Build(FakeDisk? disk = null)
    {
        disk ??= new FakeDisk();

        var state = new CashFlowAppState();
        var serializer = new CashFlowPlanJsonSerializer();

        var files = new PlanFileService(
            serializer,
            new PlanCryptoService(new ThrowingJsRuntime()),
            new PassphrasePromptService(),
            new PlanExportPreferences(new ThrowingJsRuntime()));

        var coordinator = new DiskAutoSaveCoordinator(
            state, files, disk, new UiFeedbackService(), new NullLocalizer(),
            TimeSpan.FromMilliseconds(1));

        coordinator.Initialize();

        return (coordinator, state, disk);
    }

    private static CashFlowPlan PlanWithOneAccount()
    {
        var plan = CashFlowPlanFactory.CreateEmpty("Test plan");

        plan.Accounts.Add(new Account
        {
            Name = "Main",
            Type = AccountType.BankAccount,
            Currency = "CHF",
            OpeningBalance = 100m,
            OpeningDate = new DateOnly(2026, 1, 1),
            IsActive = true
        });

        return plan;
    }

    [Fact]
    public async Task ASuccessfulDiskWrite_ClearsTheExportNeededFlag()
    {
        var (coordinator, state, disk) = Build();

        state.SetPlan(PlanWithOneAccount());

        Assert.True(state.IsDirty, "a new plan that has never been written is dirty");

        await coordinator.FlushAsync();

        Assert.Single(disk.Writes);
        Assert.False(state.IsDirty, "the plan is in a file the user controls, so nothing is pending");
        Assert.False(coordinator.FileIsBehind);
    }

    [Fact]
    public async Task AFailedDiskWrite_LeavesTheFlagSet()
    {
        var disk = new FakeDisk
        {
            NextResult = new DiskWriteResult(false, "failed", "disk full", "plan.json")
        };

        var (coordinator, state, _) = Build(disk);

        state.SetPlan(PlanWithOneAccount());

        await coordinator.FlushAsync();

        Assert.True(state.IsDirty, "nothing reached the file, so the work is still only in the browser");
        Assert.True(coordinator.FileIsBehind);
    }

    [Fact]
    public async Task ALapsedPermission_LeavesTheFlagSetAndDoesNotWrite()
    {
        var disk = new FakeDisk
        {
            NextResult = new DiskWriteResult(false, "needs-permission", "denied", "plan.json")
        };

        var (coordinator, state, _) = Build(disk);

        state.SetPlan(PlanWithOneAccount());

        await coordinator.FlushAsync();

        Assert.True(state.IsDirty);
        Assert.True(coordinator.FileIsBehind);
        Assert.Equal(DiskLinkState.NeedsPermission, disk.Status.State);
    }

    [Fact]
    public async Task NothingIsWritten_WhenNoFileIsLinked()
    {
        var disk = new FakeDisk { Status = new DiskLinkStatus(DiskLinkState.Unlinked, null) };

        var (coordinator, state, _) = Build(disk);

        state.SetPlan(PlanWithOneAccount());

        await coordinator.FlushAsync();

        Assert.Empty(disk.Writes);
        Assert.True(state.IsDirty);
    }

    [Fact]
    public async Task TheFileGetsThePlanJson_NotTheDocumentWrapper()
    {
        var (coordinator, state, disk) = Build();

        state.SetPlan(PlanWithOneAccount());

        await coordinator.FlushAsync();

        var written = Assert.Single(disk.Writes);

        Assert.Contains("\"Main\"", written);
        Assert.Contains("\"Test plan\"", written);
    }

    /// <summary>
    /// Stands in for the browser. These tests never take a path that reaches JavaScript -
    /// encryption is off, so no crypto call is made - and this makes that explicit: if a
    /// change ever routes a disk write through interop, the test fails loudly rather than
    /// quietly exercising something it was not meant to.
    /// </summary>
    private sealed class ThrowingJsRuntime : IJSRuntime
    {
        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
            throw new InvalidOperationException(
                $"The test reached JavaScript unexpectedly: {identifier}");

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier, CancellationToken cancellationToken, object?[]? args) =>
            throw new InvalidOperationException(
                $"The test reached JavaScript unexpectedly: {identifier}");
    }
}
