namespace CashFlowPlanner.Core;

public enum PaymentMethod
{
    Unknown = 0,

    BankTransfer = 1,
    StandingOrder = 2,
    Lsv = 3,
    DirectDebit = 4,
    CreditCard = 5,
    Cash = 6,
    Manual = 7,
    InternalTransfer = 8
}