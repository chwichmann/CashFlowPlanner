namespace CashFlowPlanner.Core.Validation;

public sealed class PlanValidationMessage
{
    public PlanValidationSeverity Severity { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public Guid? EntityId { get; set; }
}