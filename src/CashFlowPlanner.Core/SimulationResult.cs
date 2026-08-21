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

    /// <summary>
    /// The outstanding principal of <paramref name="mortgageId"/> as of
    /// <paramref name="date"/>, or <c>null</c> when this result knows nothing
    /// about that mortgage at all.
    ///
    /// A date BEFORE the first known point returns that first point rather than
    /// nothing. The earliest figure is the best available answer, and it is what
    /// every caller was already falling back to by hand.
    /// </summary>
    public decimal? TryGetMortgagePrincipal(Guid mortgageId, DateOnly date)
    {
        MortgagePrincipalPoint? atOrBefore = null;
        MortgagePrincipalPoint? earliest = null;

        foreach (var point in MortgagePrincipalPoints)
        {
            if (point.MortgageId != mortgageId)
            {
                continue;
            }

            if (earliest is null || point.Date < earliest.Date)
            {
                earliest = point;
            }

            if (point.Date <= date &&
                (atOrBefore is null || point.Date > atOrBefore.Date))
            {
                atOrBefore = point;
            }
        }

        return atOrBefore?.Principal ?? earliest?.Principal;
    }

    /// <summary>
    /// H1: this used to answer <c>0</c> -- "no debt" -- for any date the
    /// simulation had no principal point for, which is indistinguishable from a
    /// mortgage that is genuinely paid off. It now only answers zero when
    /// nothing at all is known about the mortgage.
    /// Prefer <see cref="TryGetMortgagePrincipal"/> when that difference matters.
    /// </summary>
    public decimal GetMortgagePrincipal(Guid mortgageId, DateOnly date)
    {
        return TryGetMortgagePrincipal(mortgageId, date) ?? 0m;
    }
}