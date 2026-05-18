namespace CashFlowPlanner.Core.Mortgages;

public sealed record MortgageBillingPeriod(
    DateOnly PeriodStart,
    DateOnly PeriodEndExclusive,
    DateOnly PaymentDate);