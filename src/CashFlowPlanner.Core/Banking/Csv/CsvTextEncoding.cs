namespace CashFlowPlanner.Core.Banking.Csv;

/// <summary>
/// Which encoding a profile expects. <see cref="Auto"/> is the honest default: Swiss CSV
/// exports are not uniformly UTF-8, and the same bank can emit UTF-8 from its web banking and
/// Latin-1 from its desktop software.
/// </summary>
public enum CsvTextEncoding
{
    Auto = 0,
    Utf8 = 1,
    Latin1 = 2
}
