namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// A parsed CAMT.053 document: <c>Document/BkToCstmrStmt</c>, one <c>GrpHdr</c> followed by one
/// or more <see cref="Statements"/>.
/// </summary>
public sealed class Camt053File
{
    /// <summary><c>GrpHdr/MsgId</c>.</summary>
    public string? MessageId { get; init; }

    /// <summary><c>GrpHdr/CreDtTm</c>.</summary>
    public DateTimeOffset? CreationDateTime { get; init; }

    /// <summary>
    /// The schema namespace the document declared, e.g.
    /// <c>urn:iso:std:iso:20022:tech:xsd:camt.053.001.08</c>. Recorded for diagnostics only -
    /// parsing matches on local names and ignores it.
    /// </summary>
    public string? SchemaNamespace { get; init; }

    /// <summary>
    /// The version suffix extracted from <see cref="SchemaNamespace"/>, e.g. <c>08</c>.
    /// <c>null</c> when the document declared no recognisable camt namespace.
    /// </summary>
    public string? SchemaVersion { get; init; }

    /// <summary>
    /// One entry per <c>Stmt</c> element. Always at least one - a document with none is
    /// rejected by the parser.
    /// </summary>
    public IReadOnlyList<Camt053Statement> Statements { get; init; } = [];
}
