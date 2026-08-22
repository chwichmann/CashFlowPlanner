using CashFlowPlanner.BlazorWasm.Services;
using CashFlowPlanner.Storage.Json;

namespace CashFlowPlanner.BlazorWasm.Tests;

/// <summary>
/// The starter plan is the first thing a new user sees. It is hand-written JSON, so nothing but a
/// test stands between a typo in it and a broken first-run experience - an invalid PaymentMethod
/// value shipped exactly that way once, and only surfaced when the button was clicked in a browser.
/// </summary>
public sealed class StarterPlanTests
{
    private static string Json()
    {
        return new StarterPlanProvider().GetJson();
    }

    [Fact]
    public void StarterPlan_Is_EmbeddedInTheAssembly()
    {
        // Guards the EmbeddedResource entry in the csproj: if that is dropped, GetJson throws
        // rather than silently returning nothing.
        Assert.False(string.IsNullOrWhiteSpace(Json()));
    }

    [Fact]
    public void StarterPlan_Deserializes()
    {
        var serializer = new CashFlowPlanJsonSerializer();

        var document = serializer.DeserializeDocument(Json());

        Assert.NotNull(document);
        Assert.Equal("Starter Plan", document.Name);
        Assert.Equal("CHF", document.BaseCurrency);
    }

    [Fact]
    public void StarterPlan_PassesPlanValidation()
    {
        var serializer = new CashFlowPlanJsonSerializer();

        var plan = serializer.DeserializeDocument(Json()).ToPlan();

        // Throws with a specific message if any referential-integrity or uniqueness rule fails.
        plan.Validate();
    }

    [Fact]
    public void StarterPlan_HasSomethingToLookAt()
    {
        // A starter plan with no accounts or movements would render an empty dashboard and teach
        // the user nothing, which defeats the point of offering it.
        var plan = new CashFlowPlanJsonSerializer()
            .DeserializeDocument(Json())
            .ToPlan();

        Assert.NotEmpty(plan.Accounts);
        Assert.NotEmpty(plan.Transactions);
        Assert.Contains(plan.Accounts, a => a.InterestContracts.Count > 0);
    }

    [Fact]
    public void StarterPlan_EveryEntityHasAVisibleName()
    {
        // The person was written with "name" while Person serializes DisplayName as
        // "displayName", so the example's only person rendered as a blank row. Nothing
        // caught it: unknown JSON fields are preserved rather than rejected, so the file
        // round-tripped and validated perfectly while showing nothing on screen.
        var plan = new CashFlowPlanJsonSerializer()
            .DeserializeDocument(Json())
            .ToPlan();

        Assert.All(plan.Accounts, a => Assert.False(string.IsNullOrWhiteSpace(a.Name)));
        Assert.All(plan.Transactions, t => Assert.False(string.IsNullOrWhiteSpace(t.Name)));
        Assert.All(plan.Persons, p => Assert.False(
            string.IsNullOrWhiteSpace(p.DisplayName),
            $"person {p.Id} has no display name - check the JSON property spelling"));
    }

    [Fact]
    public void StarterPlan_Simulates_WithoutCriticalWarnings()
    {
        var plan = new CashFlowPlanJsonSerializer()
            .DeserializeDocument(Json())
            .ToPlan();

        var result = new Core.SimulationEngine().Simulate(plan);

        Assert.NotEmpty(result.BalancePoints);

        // The example is meant to be a healthy household: if it runs its own accounts dry it is a
        // bad advertisement for the tool and a confusing first impression.
        var negative = result.Warnings
            .Where(w => w.Code == "NEGATIVE_BALANCE")
            .ToList();

        Assert.True(
            negative.Count == 0,
            $"Starter plan goes negative: {string.Join(" | ", negative.Take(3).Select(w => w.Message))}");
    }

    [Fact]
    public void StarterPlan_RoundTrips_WithoutLoss()
    {
        var serializer = new CashFlowPlanJsonSerializer();

        var first = serializer.DeserializeDocument(Json());
        var written = serializer.SerializeDocument(first);
        var second = serializer.DeserializeDocument(written);

        Assert.Equal(
            serializer.SerializeDocument(second),
            written);
    }
}
