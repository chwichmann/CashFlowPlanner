using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Core;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Finding P1b: autosave was fire-and-forget against a method with no catch, so quota,
/// validation and interop failures were invisible.
/// </summary>
public sealed class PlanCacheCoordinatorFailureTests
{
    private static (PlanCacheCoordinator Coordinator,
        CashFlowAppState State,
        FakeBrowserPlanCache Cache,
        UiFeedbackService Feedback) CreateSubject(CashFlowPlan? plan = null)
    {
        var state = new CashFlowAppState();
        var cache = new FakeBrowserPlanCache();
        var feedback = new UiFeedbackService();

        var coordinator = new PlanCacheCoordinator(
            state,
            new CashFlowPlanJsonSerializer(),
            cache,
            feedback);

        if (plan is not null)
        {
            state.SetPlan(plan);
        }

        return (coordinator, state, cache, feedback);
    }

    [Fact]
    public async Task SaveCurrentPlanAsync_Should_WriteTheWorkingCopy()
    {
        // Arrange
        var (coordinator, _, cache, feedback) =
            CreateSubject(AppStateTestPlanFactory.CreatePlan());

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.Saved, result.Outcome);
        Assert.Single(cache.Writes);
        Assert.Null(feedback.CurrentNotification);
        Assert.NotNull(coordinator.LastSuccessfulSaveAt);
    }

    [Fact]
    public async Task QuotaExceeded_Should_BeReportedToTheUser()
    {
        // Arrange
        var (coordinator, _, cache, feedback) =
            CreateSubject(AppStateTestPlanFactory.CreatePlan());

        cache.PermanentWriteResult = PlanCacheWriteResult.Failed(
            PlanCacheWriteFailure.QuotaExceeded,
            "Browser storage is full.");

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.QuotaExceeded, result.Outcome);
        Assert.True(result.IsFailure);

        Assert.Equal(UiNotificationKind.Error, feedback.CurrentNotification!.Kind);
        Assert.Contains("storage is full", feedback.CurrentNotification.Message);
        Assert.Contains("Export the plan to a file", feedback.CurrentNotification.Message);

        Assert.Null(coordinator.LastSuccessfulSaveAt);
    }

    [Fact]
    public async Task StorageUnavailable_Should_BeReportedToTheUser()
    {
        // Arrange
        var (coordinator, _, cache, feedback) =
            CreateSubject(AppStateTestPlanFactory.CreatePlan());

        cache.PermanentWriteResult = PlanCacheWriteResult.Failed(
            PlanCacheWriteFailure.StorageUnavailable,
            "Browser storage rejected the write.");

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.StorageUnavailable, result.Outcome);
        Assert.Contains("storage is unavailable", feedback.CurrentNotification!.Message);
    }

    [Fact]
    public async Task UnexpectedException_Should_NotEscapeAndShould_BeReported()
    {
        // Arrange
        var (coordinator, _, cache, feedback) =
            CreateSubject(AppStateTestPlanFactory.CreatePlan());

        cache.ThrowOnSave = new TimeoutException("interop went away");

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.Failed, result.Outcome);
        Assert.Contains("interop went away", feedback.CurrentNotification!.Message);
    }

    [Fact]
    public async Task InvalidPlan_Should_BeReportedAsAValidationFailure()
    {
        // Arrange - a plan whose default payment account points at nothing does not validate, so
        // it cannot be serialized either.
        var plan = AppStateTestPlanFactory.CreatePlan();

        var broken = new CashFlowPlan
        {
            Id = plan.Id,
            Name = plan.Name,
            BaseCurrency = plan.BaseCurrency,
            DefaultPaymentAccountId = Guid.NewGuid(),
            Persons = plan.Persons,
            Accounts = plan.Accounts,
            SimulationSettings = plan.SimulationSettings
        };

        var state = new CashFlowAppState();
        var cache = new FakeBrowserPlanCache();
        var feedback = new UiFeedbackService();

        var coordinator = new PlanCacheCoordinator(
            state,
            new CashFlowPlanJsonSerializer(),
            cache,
            feedback);

        // SetPlan does not validate, which is exactly how such a plan reaches autosave.
        state.SetPlan(broken);

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.ValidationFailed, result.Outcome);
        Assert.Empty(cache.Writes);
        Assert.Contains("not valid", feedback.CurrentNotification!.Message);
    }

    [Fact]
    public async Task NoPlan_Should_ClearTheWorkingCopy()
    {
        // Arrange
        var (coordinator, state, cache, feedback) =
            CreateSubject(AppStateTestPlanFactory.CreatePlan());

        await coordinator.SaveCurrentPlanAsync();

        state.Clear();

        // Act
        var result = await coordinator.SaveCurrentPlanAsync();

        // Assert
        Assert.Equal(PlanSaveOutcome.Cleared, result.Outcome);
        Assert.Equal(1, cache.ClearCount);
        Assert.Null(feedback.CurrentNotification);
    }
}
