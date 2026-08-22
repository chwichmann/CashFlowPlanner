using Bunit;
using CashFlowPlanner.BlazorWasm.Components;
using CashFlowPlanner.Core;

namespace CashFlowPlanner.BlazorWasm.Tests.Components;

/// <summary>
/// The engine composes its warning messages in English and always will - it is domain code with no
/// localizer in it. So the panel carries a second, localized text per code, and the engine's
/// sentence is only the fallback for a code nobody has translated yet. A code that silently showed
/// its raw identifier, or an English sentence in the middle of the German UI, is the failure this
/// covers.
/// </summary>
public sealed class SimulationWarningPanelTests : ComponentTestBase
{
    private static readonly Guid ContractId = new("30000000-0000-0000-0000-000000000001");

    private static SimulationWarning NotLinked(DateOnly date) => new()
    {
        Code = "PILLAR3A_CONTRACT_NOT_LINKED",
        Message = "Pillar 3a contract 'Saeule 3a Bank' is not linked to a Pillar 3a account.",
        Severity = WarningSeverity.Warning,
        Date = date,
        SourceId = ContractId
    };

    [Fact]
    public void A_translated_code_names_the_contract_and_says_what_it_costs()
    {
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings, [NotLinked(new DateOnly(2026, 1, 1))])
            .Add(x => x.DescribeSource, _ => "Saeule 3a Bank"));

        // The name is interpolated into the localized title rather than only appearing inside the
        // engine's English sentence.
        Assert.Contains("Saeule 3a Bank", cut.Markup, StringComparison.Ordinal);

        // And the fallback - the engine's own wording - is not what was rendered.
        Assert.DoesNotContain(
            "is not linked to a Pillar 3a account",
            cut.Markup,
            StringComparison.Ordinal);

        Assert.Contains("alert-warning", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void An_untranslated_code_still_shows_the_engines_message()
    {
        // A warning nobody got round to translating must stay readable. Showing the bare code
        // would be worse than showing an English sentence.
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings,
            [
                new SimulationWarning
                {
                    Code = "SOME_FUTURE_CODE",
                    Message = "Something specific went wrong.",
                    Severity = WarningSeverity.Warning
                }
            ]));

        Assert.Contains("Something specific went wrong.", cut.Markup, StringComparison.Ordinal);
        Assert.Contains("SOME_FUTURE_CODE", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void A_critical_warning_is_red_and_announced()
    {
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings,
            [
                new SimulationWarning
                {
                    Code = "PILLAR3A_WITHDRAWAL_NOT_POSTED",
                    Message = "…",
                    Severity = WarningSeverity.Critical,
                    Date = new DateOnly(2040, 6, 1),
                    SourceId = ContractId
                }
            ]));

        Assert.Contains("alert-danger", cut.Markup, StringComparison.Ordinal);

        // role="alert" interrupts a screen reader; the advisory ones use role="status" so that six
        // of them do not talk over each other.
        Assert.Contains("role=\"alert\"", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Repeated_occurrences_collapse_into_one_row_with_a_count()
    {
        // NEGATIVE_BALANCE is raised once per overdrawn day. Eight hundred identical red boxes is
        // a page the user scrolls past rather than a page that warns them.
        var accountId = Guid.NewGuid();

        var warnings = Enumerable.Range(0, 5)
            .Select(offset => new SimulationWarning
            {
                Code = "NEGATIVE_BALANCE",
                Message = "Balance is negative.",
                Severity = WarningSeverity.Warning,
                Date = new DateOnly(2026, 3, 1).AddDays(offset),
                AccountId = accountId
            })
            .ToList();

        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings, warnings));

        Assert.Single(cut.FindAll(".alert"));
        Assert.Contains("5", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void Codes_filters_out_everything_the_page_cannot_act_on()
    {
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings,
            [
                NotLinked(new DateOnly(2026, 1, 1)),
                new SimulationWarning
                {
                    Code = "NEGATIVE_BALANCE",
                    Message = "Balance is negative.",
                    Severity = WarningSeverity.Warning
                }
            ])
            .Add(x => x.Codes, new[] { "PILLAR3A_CONTRACT_NOT_LINKED" }));

        Assert.Single(cut.FindAll(".alert"));
        Assert.DoesNotContain("Balance is negative.", cut.Markup, StringComparison.Ordinal);
    }

    [Fact]
    public void No_warnings_renders_nothing_at_all()
    {
        // Not an empty box with a heading: a page with nothing wrong should look like a page with
        // nothing wrong.
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings, Array.Empty<SimulationWarning>()));

        Assert.Empty(cut.Markup.Trim());
    }

    [Fact]
    public void The_critical_warning_sorts_above_the_advisory_one()
    {
        var cut = Render<SimulationWarningPanel>(parameters => parameters
            .Add(x => x.Warnings,
            [
                NotLinked(new DateOnly(2026, 1, 1)),
                new SimulationWarning
                {
                    Code = "PILLAR3A_WITHDRAWAL_NOT_POSTED",
                    Message = "…",
                    Severity = WarningSeverity.Critical,
                    Date = new DateOnly(2040, 6, 1),
                    SourceId = ContractId
                }
            ]));

        var alerts = cut.FindAll(".alert");

        Assert.Equal(2, alerts.Count);
        Assert.Contains("alert-danger", alerts[0].ClassName ?? string.Empty, StringComparison.Ordinal);
    }
}
