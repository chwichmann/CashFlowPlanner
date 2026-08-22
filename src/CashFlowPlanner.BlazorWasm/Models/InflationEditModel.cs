using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Indexation;

namespace CashFlowPlanner.BlazorWasm.Models;

/// <summary>
/// The plan's inflation assumption, as the settings page edits it.
///
/// The rate stays at zero unless the user types one. A pre-filled rate - even a defensible Swiss
/// long-run 1% - would restate every figure in every existing plan the moment its owner opened
/// this page, and they would have no way of knowing which numbers moved or why.
/// </summary>
public sealed class InflationEditModel
{
    public decimal AnnualRatePercent { get; set; }

    public DateOnly? BaseDate { get; set; }

    public static InflationEditModel FromPlan(CashFlowPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new InflationEditModel
        {
            AnnualRatePercent = plan.Inflation.AnnualRatePercent,
            BaseDate = plan.Inflation.BaseDate
        };
    }

    public InflationAssumption ToAssumption()
    {
        return new InflationAssumption
        {
            AnnualRatePercent = AnnualRatePercent,

            // Kept even at a zero rate. A user who sets a rate, saves, and then zeroes it while
            // deciding should not have to re-enter the date when they put the rate back.
            BaseDate = BaseDate
        };
    }
}
