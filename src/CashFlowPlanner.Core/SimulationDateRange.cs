namespace CashFlowPlanner.Core;

public readonly record struct SimulationDateRange(
    DateOnly StartDate,
    DateOnly EndDate);