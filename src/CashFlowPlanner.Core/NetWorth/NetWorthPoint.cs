namespace CashFlowPlanner.Core.NetWorth;

/// <summary>
/// The household balance sheet on one date.
///
/// Every component is stored separately and signed the way a reader expects to
/// see it -- assets positive, liabilities positive-as-owed -- so a UI can draw a
/// stacked breakdown without re-deriving anything, and so a wrong total can be
/// traced to the one component that produced it.
///
/// <see cref="NetWorth"/> is the only derived figure.
/// </summary>
public sealed class NetWorthPoint
{
    public required DateOnly Date { get; init; }

    public required string Currency { get; init; }

    /// <summary>
    /// Bank, savings and cash balances -- money that can be spent this week.
    /// </summary>
    public required decimal LiquidAssets { get; init; }

    /// <summary>
    /// Balances on <see cref="Accounts.AccountType.Investment"/> accounts. Kept
    /// apart from <see cref="LiquidAssets"/> because a securities portfolio is
    /// an asset but not liquidity, and a household that confuses the two plans
    /// badly.
    /// </summary>
    public required decimal InvestmentAssets { get; init; }

    /// <summary>
    /// Balances on <see cref="Accounts.AccountType.Pillar3a"/> accounts.
    /// Restricted capital: real, counted, but not available before retirement
    /// (or one of the statutory early-withdrawal reasons).
    ///
    /// A Pillar 3a contract that is not linked to an account contributes
    /// nothing here -- the simulation cannot see its balance. That situation
    /// raises <c>PILLAR3A_CONTRACT_NOT_LINKED</c>.
    /// </summary>
    public required decimal Pillar3aAssets { get; init; }

    /// <summary>
    /// Assumed market value of <see cref="RealEstate.RealEstateAsset"/>s.
    /// </summary>
    public required decimal RealEstateValue { get; init; }

    /// <summary>
    /// Outstanding mortgage principal, as owed (positive). Taken from the
    /// simulation's principal points, not from any account balance.
    /// </summary>
    public required decimal MortgagePrincipal { get; init; }

    /// <summary>
    /// Everything else owed (positive): credit cards, loans, and any account
    /// flagged <see cref="Accounts.Account.IsLiability"/>. A liability account
    /// carries a negative balance, so this is its negation; an overpaid card
    /// therefore reduces the figure rather than inflating it.
    /// </summary>
    public required decimal OtherLiabilities { get; init; }

    public decimal TotalAssets =>
        LiquidAssets + InvestmentAssets + Pillar3aAssets + RealEstateValue;

    public decimal TotalLiabilities =>
        MortgagePrincipal + OtherLiabilities;

    public decimal NetWorth =>
        TotalAssets - TotalLiabilities;

    public override string ToString()
        => $"{Date:yyyy-MM-dd}: {NetWorth:N2} {Currency}";
}
