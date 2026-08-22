using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.NetWorth;

/// <summary>
/// Consolidates the two series the simulation already produced -- account
/// balances and mortgage principal -- with the plan's real-estate values into a
/// single balance-sheet series.
///
/// Before this existed a <see cref="SimulationResult"/> could tell you what was
/// in your accounts and what you still owed the bank, and there was no way to
/// put the two next to each other. A household with a CHF 700'000 mortgage and a
/// CHF 900'000 flat read as CHF 700'000 of pure debt.
///
/// Deliberate exclusions, so nobody has to guess why a number is missing:
/// <list type="bullet">
/// <item><see cref="AccountType.External"/> accounts. They exist to give
/// out-of-plan counterparties somewhere to post to; their balance is not
/// household wealth.</item>
/// <item>Pillar 2 (BVG) and AHV entitlements. Not modelled anywhere in the
/// domain, and a guessed pension capital is worse than an absent one.</item>
/// <item>Tax owed. See <c>docs/TAX-MODEL.md</c>.</item>
/// </list>
///
/// One double-count is possible and cannot be detected: a mortgage modelled BOTH
/// as a <see cref="MortgageContract"/> and as an
/// <see cref="AccountType.Mortgage"/> account with a negative balance is counted
/// once in <see cref="NetWorthPoint.MortgagePrincipal"/> and once in
/// <see cref="NetWorthPoint.OtherLiabilities"/>. A mortgage contract carries no
/// account reference, so the two cannot be reconciled here. Model the debt one
/// way or the other.
/// </summary>
public sealed class NetWorthCalculator
{
    public IReadOnlyList<NetWorthPoint> Calculate(
        CashFlowPlan plan,
        IReadOnlyCollection<Account> simulatedAccounts,
        IReadOnlyList<AccountBalancePoint> balancePoints,
        IReadOnlyList<MortgagePrincipalPoint> mortgagePrincipalPoints,
        DateOnly startDate,
        DateOnly endDate)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(simulatedAccounts);
        ArgumentNullException.ThrowIfNull(balancePoints);
        ArgumentNullException.ThrowIfNull(mortgagePrincipalPoints);

        var points = new List<NetWorthPoint>();

        if (endDate < startDate)
        {
            return points;
        }

        var accountsById = simulatedAccounts.ToDictionary(x => x.Id);

        var balancesByDate = balancePoints
            .GroupBy(x => x.Date)
            .ToDictionary(x => x.Key, x => x.ToList());

        var principalTracker = new MortgagePrincipalTracker(mortgagePrincipalPoints);

        // Nothing rounds here. Every input is already a decimal produced by the
        // engine, and this codebase has no rounding policy -- introducing one in
        // the reporting layer would make the total disagree with the components
        // it is the sum of.
        for (var date = startDate; date <= endDate; date = date.AddDays(1))
        {
            var liquid = 0m;
            var investment = 0m;
            var pillar3a = 0m;
            var otherLiabilities = 0m;

            if (balancesByDate.TryGetValue(date, out var dayBalances))
            {
                foreach (var balancePoint in dayBalances)
                {
                    if (!accountsById.TryGetValue(balancePoint.AccountId, out var account))
                    {
                        continue;
                    }

                    if (account.IsLiability)
                    {
                        otherLiabilities -= balancePoint.Balance;
                        continue;
                    }

                    switch (account.Type)
                    {
                        case AccountType.BankAccount:
                        case AccountType.SavingsAccount:
                        case AccountType.Cash:
                            liquid += balancePoint.Balance;
                            break;

                        case AccountType.Investment:
                            investment += balancePoint.Balance;
                            break;

                        case AccountType.Pillar3a:
                            pillar3a += balancePoint.Balance;
                            break;

                        case AccountType.External:
                        default:
                            break;
                    }
                }
            }

            points.Add(new NetWorthPoint
            {
                Date = date,
                Currency = plan.BaseCurrency,
                LiquidAssets = liquid,
                InvestmentAssets = investment,
                Pillar3aAssets = pillar3a,
                RealEstateValue = GetRealEstateValue(plan.RealEstateAssets, date),
                MortgagePrincipal = principalTracker.GetTotalOn(date),
                OtherLiabilities = otherLiabilities
            });
        }

        return points;
    }

    private static decimal GetRealEstateValue(
        IReadOnlyCollection<RealEstateAsset> assets,
        DateOnly date)
    {
        var total = 0m;

        foreach (var asset in assets)
        {
            total += asset.GetValueOn(date);
        }

        return total;
    }

    /// <summary>
    /// Walks the sparse principal points forward once instead of scanning them
    /// per day. A 30-year daily series against a quarterly-billed mortgage is
    /// ~11'000 dates and ~120 points; the naive lookup is 1.3 million
    /// comparisons per mortgage, and finding H6 was exactly this shape.
    ///
    /// A date before a mortgage's first known point answers that first point,
    /// matching <see cref="SimulationResult.TryGetMortgagePrincipal"/> -- the
    /// earliest figure is the best available answer, and reporting zero would
    /// read as "paid off".
    /// </summary>
    private sealed class MortgagePrincipalTracker
    {
        private readonly List<MortgagePrincipalPoint> _points;
        private readonly Dictionary<Guid, decimal> _current = [];

        private int _nextIndex;

        public MortgagePrincipalTracker(IReadOnlyList<MortgagePrincipalPoint> points)
        {
            _points = points
                .OrderBy(x => x.Date)
                .ThenBy(x => x.MortgageName)
                .ToList();

            foreach (var point in _points)
            {
                _current.TryAdd(point.MortgageId, point.Principal);
            }
        }

        public decimal GetTotalOn(DateOnly date)
        {
            while (_nextIndex < _points.Count && _points[_nextIndex].Date <= date)
            {
                var point = _points[_nextIndex];
                _current[point.MortgageId] = point.Principal;
                _nextIndex++;
            }

            var total = 0m;

            foreach (var principal in _current.Values)
            {
                total += principal;
            }

            return total;
        }
    }
}
