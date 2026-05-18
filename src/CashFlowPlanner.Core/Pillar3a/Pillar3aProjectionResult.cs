namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aProjectionResult
{
    public Guid ContractId { get; init; }

    public string ContractName { get; init; } = string.Empty;

    public Guid OwnerPersonId { get; init; }

    public IReadOnlyList<Pillar3aProjectionPoint> Points { get; init; }
        = [];
}