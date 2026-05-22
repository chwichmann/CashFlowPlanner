namespace CashFlowPlanner.Core.Banking.Import;

public enum ImportedTransactionMatchStatus
{
    Unmatched,
    MatchedAutomatically,
    MatchedManually,
    Ignored,
    SuggestedAsNewPlannedTransaction
}