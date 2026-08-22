using System.Globalization;
using System.Xml.Linq;

namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// Namespace-agnostic <see cref="XElement"/> helpers.
///
/// Every lookup matches on <see cref="XName.LocalName"/> and ignores the namespace entirely.
/// That single decision is what lets <c>camt.053.001.04</c>, <c>.001.08</c> and any future
/// <c>.09</c>/<c>.10</c> revision run through one code path: ISO bumps the namespace URI on
/// every release, but the element names in the part of the tree we read have not changed.
///
/// It is also why the generated-DTO NuGet packages are useless here - they bind to one exact
/// namespace - and why this is hand-rolled over <see cref="System.Xml.Linq"/> rather than
/// <see cref="System.Xml.Serialization.XmlSerializer"/>, which is reflection- and IL-emit-based
/// and fails only after a trimmed <c>dotnet publish</c> for Blazor WebAssembly.
/// </summary>
internal static class CamtXml
{
    /// <summary>All direct children with the given local name, in document order.</summary>
    public static IEnumerable<XElement> El(this XContainer parent, string name)
    {
        return parent
            .Elements()
            .Where(x => string.Equals(
                x.Name.LocalName,
                name,
                StringComparison.Ordinal));
    }

    /// <summary>The first direct child with the given local name, or <c>null</c>.</summary>
    public static XElement? ElFirst(this XContainer parent, string name)
    {
        return parent.El(name).FirstOrDefault();
    }

    /// <summary>
    /// Walks a chain of local names, taking the first match at every step.
    /// <c>entry.ElPath("BkTxCd", "Domn", "Cd")</c> is <c>BkTxCd/Domn/Cd</c>.
    /// </summary>
    public static XElement? ElPath(this XContainer parent, params string[] names)
    {
        XContainer? current = parent;

        foreach (var name in names)
        {
            current = current?.ElFirst(name);

            if (current is null)
            {
                return null;
            }
        }

        return current as XElement;
    }

    /// <summary>Trimmed text of the first child with the given local name; <c>null</c> when absent or blank.</summary>
    public static string? TextOf(this XContainer parent, string name)
    {
        return NullIfWhiteSpace(parent.ElFirst(name)?.Value);
    }

    /// <summary>Trimmed text at the end of an element path; <c>null</c> when any step is absent or the text is blank.</summary>
    public static string? TextPath(this XContainer parent, params string[] names)
    {
        return NullIfWhiteSpace(parent.ElPath(names)?.Value);
    }

    /// <summary>Trimmed value of an attribute; <c>null</c> when absent or blank. Attribute lookup ignores the namespace too.</summary>
    public static string? AttributeValue(this XElement element, string name)
    {
        var attribute = element
            .Attributes()
            .FirstOrDefault(x => string.Equals(
                x.Name.LocalName,
                name,
                StringComparison.Ordinal));

        return NullIfWhiteSpace(attribute?.Value);
    }

    /// <summary>All trimmed, non-blank texts of the direct children with the given local name.</summary>
    public static IReadOnlyList<string> TextsOf(this XContainer parent, string name)
    {
        return parent
            .El(name)
            .Select(x => NullIfWhiteSpace(x.Value))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();
    }

    public static string? NullIfWhiteSpace(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    /// <summary>
    /// Parses an ISO 20022 amount. Amounts are always non-negative in camt - the direction comes
    /// from <c>CdtDbtInd</c> - so a negative value is a schema violation and is rejected rather
    /// than silently absorbed, because guessing the sign is how money quietly ends up backwards.
    /// </summary>
    public static decimal ParseAmount(string rawValue, string context)
    {
        if (!decimal.TryParse(
                rawValue.Trim(),
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out var amount))
        {
            throw new Camt053ParseException(
                $"Could not parse the amount '{rawValue}' in {context}.");
        }

        if (amount < 0m)
        {
            throw new Camt053ParseException(
                $"The amount '{rawValue}' in {context} is negative. ISO 20022 amounts are unsigned; "
                + "the direction is carried by CdtDbtInd.");
        }

        return amount;
    }

    /// <summary>
    /// Reads an ISO 20022 date choice: <c>Dt</c> (xs:date) or <c>DtTm</c> (xs:dateTime).
    /// Both spellings occur in the field - UBS and PostFinance disagree on <c>ValDt</c> alone.
    /// </summary>
    public static DateOnly? ParseDateChoice(this XContainer? parent, string context)
    {
        if (parent is null)
        {
            return null;
        }

        var date = parent.TextOf("Dt");

        if (date is not null)
        {
            return ParseDateOnly(date, context);
        }

        var dateTime = parent.TextOf("DtTm");

        if (dateTime is not null)
        {
            return ParseDateOnly(dateTime, context);
        }

        return null;
    }

    public static DateOnly ParseDateOnly(string rawValue, string context)
    {
        var value = rawValue.Trim();

        if (DateOnly.TryParseExact(
                value,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var date))
        {
            return date;
        }

        if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var dateTimeOffset))
        {
            return DateOnly.FromDateTime(dateTimeOffset.DateTime);
        }

        throw new Camt053ParseException(
            $"Could not parse the date '{rawValue}' in {context}.");
    }

    public static DateTimeOffset? ParseDateTimeOffset(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            rawValue.Trim(),
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var value)
            ? value
            : null;
    }
}
