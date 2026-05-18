using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

public sealed class SimulationResult
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required IReadOnlyList<CashFlowEvent> Events { get; init; }

    public required IReadOnlyList<AccountBalancePoint> BalancePoints { get; init; }

    public required IReadOnlyList<MortgagePrincipalPoint> MortgagePrincipalPoints { get; init; }

    public required IReadOnlyList<SimulationWarning> Warnings { get; init; }

    public decimal GetBalance(Guid accountId, DateOnly date)
    {
        return BalancePoints
            .Where(x => x.AccountId == accountId && x.Date <= date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Balance)
            .FirstOrDefault();
    }

    public decimal GetMortgagePrincipal(Guid mortgageId, DateOnly date)
    {
        return MortgagePrincipalPoints
            .Where(x => x.MortgageId == mortgageId && x.Date <= date)
            .OrderByDescending(x => x.Date)
            .Select(x => x.Principal)
            .FirstOrDefault();
    }
}