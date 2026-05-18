namespace CashFlowPlanner.BlazorWasm.Components.Charts;

public sealed record TimeSeriesPoint(
    DateOnly Date,
    decimal Value);