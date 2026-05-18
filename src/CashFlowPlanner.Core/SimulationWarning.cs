namespace CashFlowPlanner.Core;

public sealed class SimulationWarning
{
    public required string Code { get; init; }

    public required string Message { get; init; }

    public WarningSeverity Severity { get; init; } = WarningSeverity.Warning;

    public DateOnly? Date { get; init; }

    public Guid? AccountId { get; init; }

    public Guid? SourceId { get; init; }
}