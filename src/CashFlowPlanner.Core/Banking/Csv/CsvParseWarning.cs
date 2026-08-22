namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Something the parser had to decide without enough evidence, or something about the file the
/// user should know before committing it.
///
/// <para>
/// These are not errors - the import proceeds - but every one of them is a place where the
/// result could be silently wrong, and a silent 100x or a rent payment booked in the wrong
/// month is exactly what the preview exists to catch.
/// </para>
/// </summary>
public enum CsvParseWarning
{
    /// <summary>Every date in the file had both components at twelve or below, so day-first was assumed.</summary>
    AmbiguousDateFormat = 0,

    /// <summary>No amount in the file settled whether "1.234" means a thousand or one. See <see cref="CsvAmountParser.Detect"/>.</summary>
    AmbiguousDecimalSeparator = 1,

    /// <summary>Rows do not all have the same number of fields as the header.</summary>
    InconsistentColumnCount = 2,

    /// <summary>A quoted field was opened and never closed. Everything after it landed in one cell.</summary>
    UnterminatedQuote = 3,

    /// <summary>No column looked like a value date, so the booking date is used for both.</summary>
    NoValueDateColumn = 4,

    /// <summary>The file names more than one currency. Every row keeps its own; the batch reports the most common.</summary>
    MixedCurrencies = 5,

    /// <summary>No balance column, so there is nothing to reconcile against. Expected for CSV, and stated rather than faked.</summary>
    NoBalanceColumn = 6,

    /// <summary>The delimiter, decimal separator or date format was inferred rather than stated by a profile.</summary>
    FormatWasAutoDetected = 7,

    /// <summary>Some rows could not be read and are listed individually.</summary>
    SomeRowsCouldNotBeRead = 8
}
