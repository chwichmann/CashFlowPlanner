namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aContributionSummary
{
    public Guid PersonId { get; set; }

    public int Year { get; set; }

    public decimal MaxAllowed { get; set; }

    public decimal Contributions { get; set; }

    public decimal Remaining => Math.Max(0m, MaxAllowed - Contributions);

    public decimal Excess => Math.Max(0m, Contributions - MaxAllowed);

    public bool IsExceeded => Excess > 0m;
}