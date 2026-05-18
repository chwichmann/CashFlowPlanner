namespace CashFlowPlanner.Core;

public enum TransactionKind
{
    ExternalIncome = 0,
    ExternalExpense = 1,
    InternalTransfer = 2,

    DebtIncrease = 10,
    DebtPayment = 11
}