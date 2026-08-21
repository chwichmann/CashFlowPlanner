using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Core.Tests.Accounts;

public sealed class AccountPostingTests
{
    private static readonly Guid FromAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid ToAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000002");
    private static readonly Guid UnrelatedAccountId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000003");

    [Theory]
    // kind, signed amount on the From account, signed amount on the To account
    [InlineData(TransactionKind.ExternalIncome, 0, 100)]
    [InlineData(TransactionKind.ExternalExpense, -100, 0)]
    [InlineData(TransactionKind.InternalTransfer, -100, 100)]
    // A debt increase adds to what is owed: the liability account moves further
    // negative even though the event points AT it.
    [InlineData(TransactionKind.DebtIncrease, 0, -100)]
    [InlineData(TransactionKind.DebtPayment, -100, 100)]
    public void GetSignedAmount_Should_MatchTheSimulationEngineSemantics(
        TransactionKind kind,
        decimal expectedOnFromAccount,
        decimal expectedOnToAccount)
    {
        var cashFlowEvent = CreateEvent(kind);

        Assert.Equal(
            expectedOnFromAccount,
            AccountPosting.GetSignedAmount(FromAccountId, cashFlowEvent));

        Assert.Equal(
            expectedOnToAccount,
            AccountPosting.GetSignedAmount(ToAccountId, cashFlowEvent));

        Assert.Equal(
            0m,
            AccountPosting.GetSignedAmount(UnrelatedAccountId, cashFlowEvent));
    }

    [Theory]
    [InlineData(TransactionKind.ExternalIncome, 1)]
    [InlineData(TransactionKind.ExternalExpense, 1)]
    [InlineData(TransactionKind.InternalTransfer, 2)]
    [InlineData(TransactionKind.DebtIncrease, 1)]
    [InlineData(TransactionKind.DebtPayment, 2)]
    public void TryGetLegs_Should_ResolveSupportedKinds(
        TransactionKind kind,
        int expectedLegCount)
    {
        Assert.True(AccountPosting.TryGetLegs(CreateEvent(kind), out var legs));
        Assert.Equal(expectedLegCount, legs.Count);
    }

    [Fact]
    public void TryGetLegs_Should_RejectUnsupportedKind()
    {
        var cashFlowEvent = CreateEvent((TransactionKind)999);

        Assert.False(AccountPosting.TryGetLegs(cashFlowEvent, out var legs));
        Assert.Empty(legs);

        Assert.Equal(0m, AccountPosting.GetSignedAmount(ToAccountId, cashFlowEvent));
    }

    [Theory]
    [InlineData(100, "increase")]
    [InlineData(-100, "decrease")]
    public void OperationName_Should_FollowTheSign(decimal signedAmount, string expected)
    {
        var leg = new AccountPostingLeg(ToAccountId, signedAmount);

        Assert.Equal(expected, leg.OperationName);
    }

    private static CashFlowEvent CreateEvent(TransactionKind kind)
    {
        return new CashFlowEvent
        {
            SourceTransactionId = Guid.NewGuid(),
            Name = "Event",
            Date = new DateOnly(2026, 6, 10),
            Kind = kind,
            FromAccountId = FromAccountId,
            ToAccountId = ToAccountId,
            Amount = 100m,
            Currency = "CHF"
        };
    }
}
