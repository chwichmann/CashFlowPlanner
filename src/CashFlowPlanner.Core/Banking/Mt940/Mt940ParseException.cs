namespace CashFlowPlanner.Core.Banking.Mt940;

public sealed class Mt940ParseException : Exception
{
    public Mt940ParseException(string message)
        : base(message)
    {
    }

    public Mt940ParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}