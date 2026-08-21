using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// Finding P1a: DeleteAccount was the only delete that skipped plan validation and only checked
/// transaction references. Deleting an account a Pillar 3a schedule pointed at succeeded, after
/// which every autosave and every export threw and the session could not be recovered.
/// </summary>
public sealed class CashFlowAppStateDeleteAccountTests
{
    private static CashFlowAppState CreateState(CashFlowPlanner.Core.CashFlowPlan plan)
    {
        var state = new CashFlowAppState();
        state.SetPlan(plan);

        return state;
    }

    [Fact]
    public void DeleteAccount_ReferencedByPillar3aSchedule_Should_BeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId)
            ]);

        var state = CreateState(plan);

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => state.DeleteAccount(AppStateTestPlanFactory.MainAccountId));

        // Assert
        Assert.Contains("Pillar 3a", exception.Message);
        Assert.Contains("Main Account", exception.Message);

        Assert.Equal(3, state.CurrentPlan!.Accounts.Count);
    }

    [Fact]
    public void DeleteAccount_ReferencedByPillar3aWithdrawalTarget_Should_BeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.SpareAccountId,
                    withdrawalTargetAccountId: AppStateTestPlanFactory.MainAccountId)
            ]);

        var state = CreateState(plan);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => state.DeleteAccount(AppStateTestPlanFactory.MainAccountId));

        Assert.Contains("withdrawal target", exception.Message);
    }

    [Fact]
    public void DeleteAccount_ReferencedByMortgagePaymentAccount_Should_BeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            mortgages:
            [
                AppStateTestPlanFactory.CreateMortgage(
                    paymentAccountId: AppStateTestPlanFactory.MainAccountId)
            ]);

        var state = CreateState(plan);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => state.DeleteAccount(AppStateTestPlanFactory.MainAccountId));

        Assert.Contains("mortgage", exception.Message);
        Assert.Contains("payment account", exception.Message);
    }

    [Fact]
    public void DeleteAccount_ReferencedByIndirectAmortisationAccount_Should_BeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            mortgages:
            [
                AppStateTestPlanFactory.CreateMortgage(
                    paymentAccountId: AppStateTestPlanFactory.MainAccountId,
                    indirectAmortisationAccountId: AppStateTestPlanFactory.SpareAccountId)
            ]);

        var state = CreateState(plan);

        // Act / Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => state.DeleteAccount(AppStateTestPlanFactory.SpareAccountId));

        Assert.Contains("indirect amortisation", exception.Message);
    }

    [Fact]
    public void DeleteAccount_ReferencedByCreditCard_Should_BeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            creditCards:
            [
                AppStateTestPlanFactory.CreateCreditCard(
                    creditCardAccountId: AppStateTestPlanFactory.CardAccountId,
                    paymentAccountId: AppStateTestPlanFactory.MainAccountId)
            ]);

        var state = CreateState(plan);

        // Act / Assert
        Assert.Contains(
            "card account",
            Assert.Throws<InvalidOperationException>(
                () => state.DeleteAccount(AppStateTestPlanFactory.CardAccountId)).Message);

        Assert.Contains(
            "payment account",
            Assert.Throws<InvalidOperationException>(
                () => state.DeleteAccount(AppStateTestPlanFactory.MainAccountId)).Message);
    }

    [Fact]
    public void DeleteAccount_ReferencedByTransaction_Should_StillBeRefused()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            transactions:
            [
                AppStateTestPlanFactory.CreateTransaction(
                    from: AppStateTestPlanFactory.MainAccountId,
                    to: AppStateTestPlanFactory.SpareAccountId)
            ]);

        var state = CreateState(plan);

        // Act / Assert
        Assert.Contains(
            "1 transaction",
            Assert.Throws<InvalidOperationException>(
                () => state.DeleteAccount(AppStateTestPlanFactory.MainAccountId)).Message);
    }

    [Fact]
    public void DeleteAccount_UsedAsDefaultPaymentAccount_Should_ClearTheDefaultAndSucceed()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan();

        var withDefault = new CashFlowPlanner.Core.CashFlowPlan
        {
            Id = plan.Id,
            Name = plan.Name,
            BaseCurrency = plan.BaseCurrency,
            DefaultPaymentAccountId = AppStateTestPlanFactory.MainAccountId,
            Persons = plan.Persons,
            Accounts = plan.Accounts,
            SimulationSettings = plan.SimulationSettings
        };

        var state = CreateState(withDefault);

        // Act
        state.DeleteAccount(AppStateTestPlanFactory.MainAccountId);

        // Assert
        Assert.Null(state.CurrentPlan!.DefaultPaymentAccountId);
        Assert.Equal(2, state.CurrentPlan.Accounts.Count);
    }

    [Fact]
    public void DeleteAccount_Unreferenced_Should_LeaveASavablePlan()
    {
        // Arrange
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId)
            ]);

        var state = CreateState(plan);
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        state.DeleteAccount(AppStateTestPlanFactory.SpareAccountId);

        // Assert - the whole point of P1a: the resulting plan still serializes.
        var json = serializer.SerializeDocument(state.GetDocumentForSave());

        Assert.Contains("\"name\": \"Test Plan\"", json);
        Assert.Equal(2, state.CurrentPlan!.Accounts.Count);
    }

    [Fact]
    public void DeleteAccount_UnknownId_Should_DoNothing()
    {
        // Arrange
        var state = CreateState(AppStateTestPlanFactory.CreatePlan());

        // Act
        state.DeleteAccount(Guid.NewGuid());

        // Assert
        Assert.Equal(3, state.CurrentPlan!.Accounts.Count);
    }
}
