using System.Xml;
using System.Xml.Linq;

namespace CashFlowPlanner.Core.Banking.Camt;

/// <summary>
/// Parser for ISO 20022 bank-to-customer statements (CAMT.053).
///
/// <para>
/// Targets <c>camt.053.001.08</c> (ISO Release 2019, Swiss Payment Standards 2026 v2.3) and
/// tolerates <c>camt.053.001.04</c>, which Swiss banks support in parallel until November 2026.
/// Both run through this one code path because every lookup matches on
/// <see cref="XName.LocalName"/> and ignores the namespace - see <see cref="CamtXml"/>.
/// </para>
///
/// <para>
/// Hand-rolled over <see cref="System.Xml.Linq"/> with no NuGet dependency. The generated-DTO
/// packages bind to a single namespace and would need swapping per revision, and
/// <c>XmlSerializer</c> is a Blazor WebAssembly trap: it is reflection- and IL-emit-based and
/// fails only after a trimmed <c>dotnet publish</c>, so it works in <c>dotnet run</c> and breaks
/// in production. <see cref="XDocument"/> has neither problem.
/// </para>
/// </summary>
public sealed class Camt053Parser
{
    private const string CamtNamespacePrefix = "urn:iso:std:iso:20022:tech:xsd:camt.053.001.";

    /// <summary>
    /// Parses the raw bytes of a camt.053 file.
    ///
    /// <para>
    /// Reading from the bytes rather than from a decoded string is deliberate: the XML
    /// declaration decides the encoding, and Swiss exports are not uniformly UTF-8 - ISO-8859-1
    /// still turns up. <see cref="XmlReader"/> honours the declaration and the byte-order mark;
    /// decoding to a string first would have to guess.
    /// </para>
    /// </summary>
    public Camt053File Parse(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length == 0)
        {
            throw new Camt053ParseException("The CAMT.053 content is empty.");
        }

        using var stream = new MemoryStream(bytes, writable: false);

        return Parse(LoadDocument(
            () => XDocument.Load(
                XmlReader.Create(stream, CreateReaderSettings()),
                LoadOptions.None)));
    }

    /// <summary>Parses camt.053 XML that has already been decoded to text.</summary>
    public Camt053File Parse(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new Camt053ParseException("The CAMT.053 content is empty.");
        }

        using var reader = new StringReader(text);

        return Parse(LoadDocument(
            () => XDocument.Load(
                XmlReader.Create(reader, CreateReaderSettings()),
                LoadOptions.None)));
    }

    /// <summary>
    /// Cheap content sniff: does this look like a camt.053 document?
    ///
    /// Used to route an upload by content rather than by file extension - Swiss banks are
    /// inconsistent about what they call the file, and MT940 exports named <c>.xml</c> and
    /// camt files named <c>.txt</c> both happen.
    /// </summary>
    public static bool LooksLikeCamt053(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        // Look only at the head of the file: enough for the declaration and the root element,
        // and it keeps the check O(1) for a multi-megabyte statement.
        var head = text.Length <= 4096
            ? text
            : text[..4096];

        return head.Contains("BkToCstmrStmt", StringComparison.Ordinal)
            || head.Contains("camt.053", StringComparison.Ordinal);
    }

    private static XmlReaderSettings CreateReaderSettings()
    {
        return new XmlReaderSettings
        {
            // A bank statement is untrusted input from the user's disk. No DTD means no
            // billion-laughs expansion; no resolver means no external entity fetch - which
            // also keeps the app's "no network calls" property intact.
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = false
        };
    }

    private static XDocument LoadDocument(Func<XDocument> load)
    {
        try
        {
            return load();
        }
        catch (XmlException exception)
        {
            throw new Camt053ParseException(
                $"The file is not valid XML (line {exception.LineNumber}, position "
                + $"{exception.LinePosition}): {exception.Message}",
                exception);
        }
    }

    private static Camt053File Parse(XDocument document)
    {
        var root = document.Root
            ?? throw new Camt053ParseException("The CAMT.053 document has no root element.");

        // Accept the document element itself or any wrapper around it: bLink and some bank
        // portals deliver the camt inside an envelope.
        var bankToCustomerStatement = root
            .DescendantsAndSelf()
            .FirstOrDefault(x => string.Equals(
                x.Name.LocalName,
                "BkToCstmrStmt",
                StringComparison.Ordinal))
            ?? throw new Camt053ParseException(
                $"No 'BkToCstmrStmt' element found. The root element is '{root.Name.LocalName}'. "
                + "This does not look like a CAMT.053 bank-to-customer statement.");

        var groupHeader = bankToCustomerStatement.ElFirst("GrpHdr");

        var schemaNamespace = CamtXml.NullIfWhiteSpace(
            root.Name.NamespaceName);

        var statements = bankToCustomerStatement
            .El("Stmt")
            .Select(ParseStatement)
            .ToList();

        if (statements.Count == 0)
        {
            throw new Camt053ParseException(
                "The CAMT.053 document contains no 'Stmt' element. At least one statement is required.");
        }

        return new Camt053File
        {
            MessageId = groupHeader?.TextOf("MsgId"),
            CreationDateTime = CamtXml.ParseDateTimeOffset(groupHeader?.TextOf("CreDtTm")),
            SchemaNamespace = schemaNamespace,
            SchemaVersion = ExtractSchemaVersion(schemaNamespace),
            Statements = statements
        };
    }

    private static string? ExtractSchemaVersion(string? schemaNamespace)
    {
        if (schemaNamespace is null ||
            !schemaNamespace.StartsWith(CamtNamespacePrefix, StringComparison.Ordinal))
        {
            return null;
        }

        return CamtXml.NullIfWhiteSpace(
            schemaNamespace[CamtNamespacePrefix.Length..]);
    }

    private static Camt053Statement ParseStatement(XElement statementElement)
    {
        var account = statementElement.ElFirst("Acct");

        var iban = account?.TextPath("Id", "IBAN");
        var otherAccountId = account?.TextPath("Id", "Othr", "Id");

        // Acct/Ccy is the fallback for entries whose Amt carries no Ccy attribute.
        var accountCurrency = account?.TextOf("Ccy") ?? "CHF";

        var period = statementElement.ElFirst("FrToDt");

        var balances = statementElement
            .El("Bal")
            .Select(x => ParseBalance(x, accountCurrency))
            .ToList();

        var entries = statementElement
            .El("Ntry")
            .Select(x => ParseEntry(x, accountCurrency))
            .ToList();

        var openingBalance =
            FindBalance(balances, "OPBD")
            ?? FindBalance(balances, "PRCD");

        var closingBalance = FindBalance(balances, "CLBD");

        return new Camt053Statement
        {
            Id = statementElement.TextOf("Id"),
            ElectronicSequenceNumber = statementElement.TextOf("ElctrncSeqNb"),
            LegalSequenceNumber = statementElement.TextOf("LglSeqNb"),
            CreationDateTime = CamtXml.ParseDateTimeOffset(statementElement.TextOf("CreDtTm")),
            FromDate = ParseOptionalDate(period?.TextOf("FrDtTm"), "Stmt/FrToDt/FrDtTm"),
            ToDate = ParseOptionalDate(period?.TextOf("ToDtTm"), "Stmt/FrToDt/ToDtTm"),
            Iban = iban,
            OtherAccountIdentification = otherAccountId,
            Currency = accountCurrency,
            AccountOwnerName = ReadPartyName(account?.ElFirst("Ownr")),
            // BIC in camt.053.001.04, BICFI in .08 - the element was renamed between revisions.
            ServicerBic =
                account?.TextPath("Svcr", "FinInstnId", "BICFI")
                ?? account?.TextPath("Svcr", "FinInstnId", "BIC"),
            Balances = balances,
            OpeningBalance = openingBalance,
            ClosingBalance = closingBalance,
            Entries = entries,
            Reconciliation = Camt053ReconciliationResult.Create(
                openingBalance,
                closingBalance,
                entries)
        };
    }

    private static Camt053Balance? FindBalance(
        IReadOnlyList<Camt053Balance> balances,
        string typeCode)
    {
        return balances.FirstOrDefault(x => string.Equals(
            x.TypeCode,
            typeCode,
            StringComparison.OrdinalIgnoreCase));
    }

    private static Camt053Balance ParseBalance(
        XElement balanceElement,
        string accountCurrency)
    {
        var codeOrProprietary = balanceElement.ElPath("Tp", "CdOrPrtry");

        // Cd/Prtry is a schema choice; read both spellings.
        var typeCode =
            codeOrProprietary?.TextOf("Cd")
            ?? codeOrProprietary?.TextOf("Prtry")
            ?? string.Empty;

        var amountElement = balanceElement.ElFirst("Amt")
            ?? throw new Camt053ParseException(
                $"The balance '{typeCode}' has no 'Amt' element.");

        var date = balanceElement.ElFirst("Dt").ParseDateChoice($"Bal[{typeCode}]/Dt")
            ?? throw new Camt053ParseException(
                $"The balance '{typeCode}' has no usable date.");

        return new Camt053Balance
        {
            TypeCode = typeCode,
            Date = date,
            CreditDebitIndicator = ParseCreditDebitIndicator(
                balanceElement.TextOf("CdtDbtInd"),
                $"Bal[{typeCode}]"),
            Amount = CamtXml.ParseAmount(amountElement.Value, $"Bal[{typeCode}]/Amt"),
            Currency = amountElement.AttributeValue("Ccy") ?? accountCurrency
        };
    }

    private static Camt053Entry ParseEntry(
        XElement entryElement,
        string accountCurrency)
    {
        var accountServicerReference = entryElement.TextOf("AcctSvcrRef");

        var context = accountServicerReference is null
            ? "Ntry"
            : $"Ntry[{accountServicerReference}]";

        var amountElement = entryElement.ElFirst("Amt")
            ?? throw new Camt053ParseException($"{context} has no 'Amt' element.");

        var bookingDate = entryElement.ElFirst("BookgDt").ParseDateChoice($"{context}/BookgDt");
        var valueDate = entryElement.ElFirst("ValDt").ParseDateChoice($"{context}/ValDt");

        var effectiveValueDate = valueDate
            ?? bookingDate
            ?? throw new Camt053ParseException(
                $"{context} has neither a value date nor a booking date.");

        // NtryDtls is 0..n and TxDtls is 0..n within it. Flattening is safe precisely because
        // the details never contribute to the amount - the entry amount is the whole booking.
        var details = entryElement
            .El("NtryDtls")
            .SelectMany(x => x.El("TxDtls"))
            .Select(x => ParseTransactionDetail(x, accountCurrency))
            .ToList();

        var charges = ParseCharges(entryElement.ElFirst("Chrgs"), context);

        return new Camt053Entry
        {
            AccountServicerReference = accountServicerReference,
            EntryReference = entryElement.TextOf("NtryRef"),
            BookingDate = bookingDate,
            ValueDate = effectiveValueDate,
            CreditDebitIndicator = ParseCreditDebitIndicator(
                entryElement.TextOf("CdtDbtInd"),
                context),
            Amount = CamtXml.ParseAmount(amountElement.Value, $"{context}/Amt"),
            // Ccy is an attribute of Amt, not a child element.
            Currency = amountElement.AttributeValue("Ccy") ?? accountCurrency,
            Status = ReadStatus(entryElement),
            IsReversal = ParseBoolean(entryElement.TextOf("RvslInd")),
            BankTransactionCode = ReadBankTransactionCode(entryElement.ElFirst("BkTxCd")),
            TotalCharges = charges.Amount,
            ChargesCurrency = charges.Currency,
            AdditionalEntryInformation = entryElement.TextOf("AddtlNtryInf"),
            Details = details,
            RawXml = entryElement.ToString(SaveOptions.DisableFormatting)
        };
    }

    /// <summary>
    /// <c>Sts</c> is a plain code in camt.053.001.04 and a <c>Cd</c>/<c>Prtry</c> choice
    /// wrapper in .08 - another element that changed shape between the two revisions in use.
    /// </summary>
    private static string? ReadStatus(XElement entryElement)
    {
        var status = entryElement.ElFirst("Sts");

        if (status is null)
        {
            return null;
        }

        return status.TextOf("Cd")
            ?? status.TextOf("Prtry")
            ?? CamtXml.NullIfWhiteSpace(status.Value);
    }

    private static Camt053TransactionDetail ParseTransactionDetail(
        XElement detailElement,
        string accountCurrency)
    {
        var references = detailElement.ElFirst("Refs");
        var amountDetails = detailElement.ElFirst("AmtDtls");
        var relatedParties = detailElement.ElFirst("RltdPties");
        var remittanceInformation = detailElement.ElFirst("RmtInf");

        // TxDtls/Amt in .08; TxDtls/AmtDtls/TxAmt/Amt is the common alternative placement.
        var amountElement =
            detailElement.ElFirst("Amt")
            ?? amountDetails?.ElPath("TxAmt", "Amt")
            ?? amountDetails?.ElPath("InstdAmt", "Amt");

        var creditDebitIndicator = ParseOptionalCreditDebitIndicator(
            detailElement.TextOf("CdtDbtInd"));

        var creditorReference = ReadCreditorReference(remittanceInformation);

        var unstructured = remittanceInformation is null
            ? null
            : CamtXml.NullIfWhiteSpace(
                string.Join(" ", remittanceInformation.TextsOf("Ustrd")));

        var charges = ParseCharges(detailElement.ElFirst("Chrgs"), "TxDtls");

        return new Camt053TransactionDetail
        {
            AccountServicerReference = references?.TextOf("AcctSvcrRef"),
            EndToEndId = NormalizeReference(references?.TextOf("EndToEndId")),
            InstructionId = NormalizeReference(references?.TextOf("InstrId")),
            MandateId = NormalizeReference(references?.TextOf("MndtId")),
            Uetr = references?.TextOf("UETR"),
            Amount = amountElement is null
                ? null
                : CamtXml.ParseAmount(amountElement.Value, "TxDtls/Amt"),
            CreditDebitIndicator = creditDebitIndicator,
            Currency = amountElement?.AttributeValue("Ccy") ?? accountCurrency,
            CreditorReferenceType = creditorReference.Type,
            CreditorReference = creditorReference.Reference,
            UnstructuredRemittanceInformation = unstructured,
            AdditionalTransactionInformation = detailElement.TextOf("AddtlTxInf"),
            CreditorName = ReadPartyName(relatedParties?.ElFirst("Cdtr")),
            CreditorIban = relatedParties?.TextPath("CdtrAcct", "Id", "IBAN"),
            DebtorName = ReadPartyName(relatedParties?.ElFirst("Dbtr")),
            DebtorIban = relatedParties?.TextPath("DbtrAcct", "Id", "IBAN"),
            TotalCharges = charges.Amount
        };
    }

    /// <summary>
    /// Reads a party name across both revisions in use: camt.053.001.04 puts <c>Nm</c> directly
    /// under <c>Cdtr</c>/<c>Dbtr</c>, while .08 wraps the party in a <c>Pty</c> choice element.
    /// </summary>
    private static string? ReadPartyName(XElement? partyElement)
    {
        if (partyElement is null)
        {
            return null;
        }

        return partyElement.TextOf("Nm")
            ?? partyElement.TextPath("Pty", "Nm");
    }

    /// <summary>
    /// Reads the structured creditor reference. <c>Tp/CdOrPrtry</c> is a schema *choice*, and
    /// which half is populated depends on the payment scheme:
    /// <list type="bullet">
    ///   <item>QR-IBAN: <c>Prtry</c> = <c>QRR</c>, <c>Ref</c> is the 27-digit QR reference.</item>
    ///   <item>Normal IBAN with an ISO Creditor Reference: <c>Cd</c> = <c>SCOR</c>, <c>Ref</c> is
    ///         an ISO 11649 <c>RFxx...</c>.</item>
    ///   <item>LSV+/BDD: <c>Prtry</c> = <c>ISR Reference</c>.</item>
    /// </list>
    /// Checking only one spelling silently loses every reference of the other kind.
    /// </summary>
    private static (string? Type, string? Reference) ReadCreditorReference(
        XElement? remittanceInformation)
    {
        if (remittanceInformation is null)
        {
            return (null, null);
        }

        foreach (var structured in remittanceInformation.El("Strd"))
        {
            var referenceInformation = structured.ElFirst("CdtrRefInf");

            if (referenceInformation is null)
            {
                continue;
            }

            var reference = referenceInformation.TextOf("Ref");

            if (reference is null)
            {
                continue;
            }

            var codeOrProprietary = referenceInformation.ElPath("Tp", "CdOrPrtry");

            var type =
                codeOrProprietary?.TextOf("Prtry")
                ?? codeOrProprietary?.TextOf("Cd");

            return (type, reference);
        }

        return (null, null);
    }

    /// <summary>
    /// <c>Chrgs</c> across the revisions in use: <c>TtlChrgsAndTaxAmt</c> in .08, a bare
    /// <c>Amt</c> in older shapes, or a set of <c>Rcrd</c> entries to be totalled.
    ///
    /// The value is informational. <c>Ntry/Amt</c> is the amount actually booked and already
    /// nets any charge deducted from it, so charges are never added to or removed from the sum.
    /// </summary>
    private static (decimal? Amount, string? Currency) ParseCharges(
        XElement? chargesElement,
        string context)
    {
        if (chargesElement is null)
        {
            return (null, null);
        }

        var totalElement =
            chargesElement.ElFirst("TtlChrgsAndTaxAmt")
            ?? chargesElement.ElFirst("Amt");

        if (totalElement is not null)
        {
            return (
                CamtXml.ParseAmount(totalElement.Value, $"{context}/Chrgs"),
                totalElement.AttributeValue("Ccy"));
        }

        var records = chargesElement
            .El("Rcrd")
            .Select(x => x.ElFirst("Amt"))
            .Where(x => x is not null)
            .Select(x => x!)
            .ToList();

        if (records.Count == 0)
        {
            return (null, null);
        }

        return (
            records.Sum(x => CamtXml.ParseAmount(x.Value, $"{context}/Chrgs/Rcrd/Amt")),
            records[0].AttributeValue("Ccy"));
    }

    private static string ReadBankTransactionCode(XElement? bankTransactionCodeElement)
    {
        if (bankTransactionCodeElement is null)
        {
            return string.Empty;
        }

        var domain = bankTransactionCodeElement.ElFirst("Domn");

        if (domain is not null)
        {
            var family = domain.ElFirst("Fmly");

            var parts = new[]
                {
                    domain.TextOf("Cd"),
                    family?.TextOf("Cd"),
                    family?.TextOf("SubFmlyCd")
                }
                .Where(x => x is not null)
                .Select(x => x!)
                .ToList();

            if (parts.Count > 0)
            {
                return string.Join("-", parts);
            }
        }

        // Some banks send only a proprietary code, e.g. PostFinance's own booking types.
        return bankTransactionCodeElement.TextPath("Prtry", "Cd")
            ?? string.Empty;
    }

    private static Camt053CreditDebitIndicator ParseCreditDebitIndicator(
        string? value,
        string context)
    {
        return ParseOptionalCreditDebitIndicator(value)
            ?? throw new Camt053ParseException(
                value is null
                    ? $"{context} has no 'CdtDbtInd' element. camt carries the sign there and nowhere else."
                    : $"Unsupported CdtDbtInd '{value}' in {context}. Expected 'CRDT' or 'DBIT'.");
    }

    private static Camt053CreditDebitIndicator? ParseOptionalCreditDebitIndicator(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "CRDT" => Camt053CreditDebitIndicator.Credit,
            "DBIT" => Camt053CreditDebitIndicator.Debit,
            _ => null
        };
    }

    private static bool ParseBoolean(string? value)
    {
        if (value is null)
        {
            return false;
        }

        var trimmed = value.Trim();

        return string.Equals(trimmed, "true", StringComparison.OrdinalIgnoreCase)
            || string.Equals(trimmed, "1", StringComparison.Ordinal);
    }

    private static DateOnly? ParseOptionalDate(string? value, string context)
    {
        return value is null
            ? null
            : CamtXml.ParseDateOnly(value, context);
    }

    /// <summary>
    /// Drops the ISO placeholders banks send when a reference does not exist. Keeping them turns
    /// "no reference" into a value that looks real in the UI and in the deduplication fallback.
    /// </summary>
    private static string? NormalizeReference(string? value)
    {
        if (value is null)
        {
            return null;
        }

        return string.Equals(value, "NOTPROVIDED", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "NONREF", StringComparison.OrdinalIgnoreCase)
            ? null
            : value;
    }
}
