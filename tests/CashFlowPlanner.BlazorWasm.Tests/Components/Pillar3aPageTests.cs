using Bunit;
using CashFlowPlanner.BlazorWasm.Pages;
using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// Finding H8, from the UI side.
///
/// A Pillar 3a contract with no <c>AccountId</c> has its contributions debited from the payment
/// account and credited to nothing: the money leaves the plan and the household looks poorer for
/// saving. The domain made the link possible; these tests are about the editor making it the
/// obvious thing to do, and about the editor not destroying anything on the way.
/// </summary>
public sealed class Pillar3aPageTests : PageTestBase
{
    private static readonly Guid ContractId = new("40000000-0000-0000-0000-000000000001");

    private static CashFlowPlan PlanWithUnlinkedContract() =>
        AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId,
                    id: ContractId)
            ],
            withPillar3aAccount: true);

    [Fact]
    public void An_unlinked_contract_is_marked_as_such_in_the_list()
    {
        LoadPlan(PlanWithUnlinkedContract());

        var cut = Render<Pillar3a>();

        // Not a quiet empty cell: an unlinked contract is a contract whose money is being thrown
        // away, and the list is where a user looks first.
        Assert.Contains("badge bg-warning", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void The_account_picker_offers_Pillar3a_accounts_and_nothing_else()
    {
        LoadPlan(PlanWithUnlinkedContract());

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();

        var picker = cut.FindAll("select")
            .Single(x => x.QuerySelectorAll("option")
                .Any(option => option.TextContent.Contains("Pillar 3a Account", StringComparison.Ordinal)));

        var options = picker.QuerySelectorAll("option")
            .Select(x => x.TextContent.Trim())
            .ToList();

        // The "not linked" placeholder plus the one account of the right type. A bank account
        // offered here would produce a plan that fails validation on the next save.
        Assert.Equal(2, options.Count);
        Assert.Contains("Pillar 3a Account", options, StringComparer.Ordinal);
        Assert.DoesNotContain("Main Account", options, StringComparer.Ordinal);
        Assert.DoesNotContain("Visa", options, StringComparer.Ordinal);
    }

    [Fact]
    public void Linking_the_account_and_saving_writes_it_to_the_plan()
    {
        LoadPlan(PlanWithUnlinkedContract());

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();

        var picker = cut.FindAll("select")
            .Single(x => x.QuerySelectorAll("option")
                .Any(option => option.TextContent.Contains("Pillar 3a Account", StringComparison.Ordinal)));

        picker.Change(AppStateTestPlanFactory.Pillar3aAccountId.ToString());

        FindButton(cut, "Save").Click();

        Assert.Equal(
            AppStateTestPlanFactory.Pillar3aAccountId,
            AppState.CurrentPlan!.Pillar3aContracts.Single().AccountId);
    }

    [Fact]
    public void The_editor_creates_the_Pillar3a_account_inline_and_links_it()
    {
        // A user with no Pillar 3a account has nothing to pick, and sending them to the Accounts
        // page to come back afterwards is exactly the detour that leaves contracts unlinked.
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId,
                    id: ContractId)
            ]);

        LoadPlan(plan);

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();
        FindButton(cut, "Create Pillar 3a account").Click();

        // AccountValidator rejects a Pillar 3a account without exactly one owner and a subtype, so
        // the dialog asks for both. The owner is pre-filled from the contract; the subtype is a
        // real choice and is not.
        var subtypePicker = cut.FindAll("select")
            .Single(x => x.QuerySelectorAll("option")
                .Any(option => option.TextContent.Contains("Fund solution", StringComparison.OrdinalIgnoreCase)));

        subtypePicker.Change(Pillar3aAccountSubtype.FundSolution.ToString());

        FindButton(cut, "Save", ".modal-content").Click();

        var account = AppState.CurrentPlan!.Accounts.Single(x => x.Type == AccountType.Pillar3a);

        Assert.Equal(Pillar3aAccountSubtype.FundSolution, account.Pillar3aSubtype);
        Assert.Single(account.Owners);
        Assert.Equal(AppStateTestPlanFactory.PersonId, account.Owners[0].PersonId);

        // Created and linked, in one step: the contract editor is still open with the new account
        // selected, and saving commits the link.
        FindButton(cut, "Save").Click();

        Assert.Equal(account.Id, AppState.CurrentPlan.Pillar3aContracts.Single().AccountId);
    }

    [Fact]
    public void The_inline_account_dialog_refuses_to_save_without_a_subtype()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId,
                    id: ContractId)
            ]));

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();
        FindButton(cut, "Create Pillar 3a account").Click();
        FindButton(cut, "Save", ".modal-content").Click();

        // Refused here rather than at the next export, which is where AccountValidator would
        // otherwise raise it - long after the dialog that caused it has been closed.
        Assert.DoesNotContain(AppState.CurrentPlan!.Accounts, x => x.Type == AccountType.Pillar3a);
        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Editing_a_contract_does_not_delete_its_withdrawals()
    {
        // The editor wrote Withdrawals = [] on every save. Withdrawals were persisted, validated
        // and - since wave 4 - simulated, but had no editor, so opening a contract that carried
        // any and pressing Save deleted a planned retirement payout without a word.
        var plan = AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId,
                    withdrawalTargetAccountId: AppStateTestPlanFactory.SpareAccountId,
                    id: ContractId)
            ]);

        LoadPlan(plan);

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();
        FindButton(cut, "Save").Click();

        var withdrawal = Assert.Single(AppState.CurrentPlan!.Pillar3aContracts.Single().Withdrawals);

        Assert.Equal(1000m, withdrawal.Amount);
        Assert.Equal(AppStateTestPlanFactory.SpareAccountId, withdrawal.TargetAccountId);
    }

    [Fact]
    public void A_withdrawal_can_be_added_from_the_editor()
    {
        LoadPlan(AppStateTestPlanFactory.CreatePlan(
            pillar3aContracts:
            [
                AppStateTestPlanFactory.CreatePillar3aContract(
                    contributionAccountId: AppStateTestPlanFactory.MainAccountId,
                    id: ContractId)
            ]));

        var cut = Render<Pillar3a>();

        FindButton(cut, "Edit").Click();
        FindButton(cut, "Add withdrawal").Click();

        // Core rejects a withdrawal with neither an amount nor CloseContract, which is what a
        // freshly added row is - so the amount has to be filled in before saving.
        var amountInput = cut.Find(".pillar3a-withdrawal input[inputmode='decimal']");
        amountInput.Change("2500");

        FindButton(cut, "Save").Click();

        Assert.Equal(2500m, AppState.CurrentPlan!.Pillar3aContracts.Single().Withdrawals.Single().Amount);
    }

    [Fact]
    public void The_not_linked_warning_is_shown_at_the_top_with_a_way_to_fix_it()
    {
        LoadPlan(PlanWithUnlinkedContract());

        // The warning comes out of a real run rather than a hand-made SimulationWarning, so the
        // code the panel filters on is the code the engine actually emits.
        AppState.RunSimulation();

        var cut = Render<Pillar3a>();

        Assert.Contains(
            "PILLAR3A_CONTRACT_NOT_LINKED",
            AppState.CurrentSimulationResult!.Warnings.Select(x => x.Code));

        var alert = cut.FindAll(".alert-warning")
            .First(x => x.TextContent.Contains("Pillar 3a Fund", StringComparison.Ordinal));

        Assert.Contains("Open contract", alert.TextContent, StringComparison.Ordinal);
    }
}
