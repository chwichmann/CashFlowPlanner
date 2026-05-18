namespace CashFlowPlanner.Core.Pillar3a;

public sealed class Pillar3aProjectionAssumption
{
    public Pillar3aProjectionMethod Method { get; init; } = Pillar3aProjectionMethod.ExpectedReturn;

    public decimal ExpectedAnnualReturnPercent { get; init; }

    public decimal AnnualFeePercent { get; init; }

    public decimal? GuaranteedPayoutAtRetirement { get; init; }

    public decimal? ExpectedSurplusPercent { get; init; }

    public void Validate(string contractName)
    {
        if (ExpectedAnnualReturnPercent < -100m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{contractName}' has an invalid expected annual return.");
        }

        if (AnnualFeePercent < 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{contractName}' has a negative annual fee.");
        }

        if (GuaranteedPayoutAtRetirement is not null &&
            GuaranteedPayoutAtRetirement.Value < 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{contractName}' has a negative guaranteed payout.");
        }

        if (ExpectedSurplusPercent is not null &&
            ExpectedSurplusPercent.Value < 0m)
        {
            throw new InvalidOperationException(
                $"Pillar 3a contract '{contractName}' has a negative expected surplus percentage.");
        }
    }
}