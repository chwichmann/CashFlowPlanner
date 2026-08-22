namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// Thrown for any content the CAMT.053 parser cannot make sense of.
///
/// Every failure path funnels through this type - including malformed XML, which is caught
/// and re-thrown here with the line and position - so the import UI can show one actionable
/// message instead of an unhandled <see cref="System.Xml.XmlException"/>.
/// </summary>
public sealed class Camt053ParseException : Exception
{
    public Camt053ParseException(string message)
        : base(message)
    {
    }

    public Camt053ParseException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
