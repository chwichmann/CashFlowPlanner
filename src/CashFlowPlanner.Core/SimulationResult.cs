using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Indexation;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.NetWorth;

public sealed class SimulationResult
{
    public required DateOnly StartDate { get; init; }

    public required DateOnly EndDate { get; init; }

    public required IReadOnlyList<CashFlowEvent> Events { get; init; }

    public required IReadOnlyList<AccountBalancePoint> BalancePoints { get; init; }

    public required IReadOnlyList<MortgagePrincipalPoint> MortgagePrincipalPoints { get; init; }

    public required IReadOnlyList<SimulationWarning> Warnings { get; init; }

    /// <summary>
    /// The household balance sheet, one point per simulated day, with its
    /// components kept separable. Defaults to empty so a hand-built result -- a
    /// test fixture, say -- does not have to fabricate one.
    /// </summary>
    public IReadOnlyList<NetWorthPoint> NetWorthPoints { get; init; } = [];

    /// <summary>
    /// The inflation assumption the plan was simulated under. Every amount in
    /// this result is NOMINAL -- francs of the day the money moves. This is what
    /// lets a caller convert to real terms without knowing the plan.
    /// </summary>
    public InflationAssumption Inflation { get; init; } = new();

    /// <summary>
    /// <paramref name="nominalAmount"/>, observed on <paramref name="date"/>,
    /// expressed in the requested basis.
    ///
    /// Real vs nominal is deliberately a presentation concern and never a change
    /// to what the engine computed: turning inflation on does not silently
    /// redefine what an existing number means, it adds a second way to read it.
    /// With no inflation assumption the two bases are identical.
    /// </summary>
    public decimal ToBasis(
        decimal nominalAmount,
        DateOnly date,
        AmountBasis basis)
    {
        if (basis == AmountBasis.Nominal || !Inflation.IsEnabled)
        {
            return nominalAmount;
        }

        return AnnualCompounding.Deflate(
            nominalAmount,
            Inflation.AnnualRatePercent,
            Inflation.BaseDate!.Value,
            date);
    }

    /// <summary>
    /// The net-worth series in the requested basis. In
    /// <see cref="AmountBasis.Real"/> every point is deflated back to the
    /// plan's inflation base date, so the series answers "what is this worth in
    /// today's money" rather than "what number will the statement show".
    /// </summary>
    public IReadOnlyList<NetWorthPoint> GetNetWorthPoints(AmountBasis basis)
    {
        if (basis == AmountBasis.Nominal || !Inflation.IsEnabled)
        {
            return NetWorthPoints;
        }

        var rate = Inflation.AnnualRatePercent;
        var baseDate = Inflation.BaseDate!.Value;

        return NetWorthPoints
            .Select(point =>
            {
                var factor = AnnualCompounding.Factor(rate, baseDate, point.Date);

                return factor == 0m ? point : point.Scale(1m / factor);
            })
            .ToList();
    }

    /// <summary>
    /// The balance sheet on <paramref name="date"/>, or <c>null</c> when the
    /// date is outside the simulated range.
    /// </summary>
    public NetWorthPoint? TryGetNetWorth(DateOnly date)
    {
        NetWorthPoint? atOrBefore = null;

        foreach (var point in NetWorthPoints)
        {
            if (point.Date > date)
            {
                continue;
            }

            if (atOrBefore is null || point.Date > atOrBefore.Date)
            {
                atOrBefore = point;
            }
        }

        return atOrBefore;
    }

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