using CashFlowPlanner.Core.Banking.Camt;

namespace CashFlowPlanner.Core.Tests.Banking.Camt;

public sealed class Camt053ParserTests
{
    private static Camt053File Parse(string fixtureName)
    {
        return new Camt053Parser().Parse(
            CamtFixture.ReadBytes(fixtureName));
    }

    private static Camt053Statement ParseSingleStatement(string fixtureName)
    {
        var file = Parse(fixtureName);

        return Assert.Single(file.Statements);
    }

    [Fact]
    public void Parse_ReadsGroupHeaderAndStatementMetadata()
    {
        var file = Parse(CamtFixture.Plain08);

        Assert.Equal("20260124-CAMT053-0001", file.MessageId);
        Assert.Equal("08", file.SchemaVersion);
        Assert.Equal(
            "urn:iso:std:iso:20022:tech:xsd:camt.053.001.08",
            file.SchemaNamespace);

        var statement = Assert.Single(file.Statements);

        Assert.Equal("STMT-2026-004", statement.Id);
        Assert.Equal("4", statement.ElectronicSequenceNumber);
        Assert.Equal("142", statement.LegalSequenceNumber);
        Assert.Equal("CH2100210210108311400", statement.Iban);
        Assert.Equal("CH2100210210108311400", statement.AccountIdentifier);
        Assert.Equal("CHF", statement.Currency);
        Assert.Equal("Christian Wichmann", statement.AccountOwnerName);
        Assert.Equal("UBSWCHZH80A", statement.ServicerBic);
        Assert.Equal(new DateOnly(2026, 1, 1), statement.FromDate);
        Assert.Equal(new DateOnly(2026, 1, 23), statement.ToDate);
    }

    [Fact]
    public void Parse_PicksOpeningAndClosingBookedBalances_AndIgnoresClosingAvailable()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        Assert.Equal(3, statement.Balances.Count);

        Assert.NotNull(statement.OpeningBalance);
        Assert.Equal("OPBD", statement.OpeningBalance.TypeCode);
        Assert.Equal(4042.62m, statement.OpeningBalance.SignedAmount);
        Assert.Equal(new DateOnly(2026, 1, 1), statement.OpeningBalance.Date);

        Assert.NotNull(statement.ClosingBalance);
        Assert.Equal("CLBD", statement.ClosingBalance.TypeCode);
        Assert.Equal(4978.37m, statement.ClosingBalance.SignedAmount);

        // CLAV is present in the fixture and is 2'000 higher. Using it would make every
        // reconciliation fail and every suggested balance wrong.
        Assert.Contains(statement.Balances, x => x.TypeCode == "CLAV");
        Assert.NotEqual(6978.37m, statement.ClosingBalance.SignedAmount);
    }

    [Fact]
    public void Parse_AppliesSignFromCreditDebitIndicator_InBothDirections()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        var debit = statement.Entries[0];
        var credit = statement.Entries[1];

        Assert.Equal(Camt053CreditDebitIndicator.Debit, debit.CreditDebitIndicator);
        Assert.Equal(40.00m, debit.Amount);
        Assert.Equal(-40.00m, debit.SignedAmount);

        Assert.Equal(Camt053CreditDebitIndicator.Credit, credit.CreditDebitIndicator);
        Assert.Equal(975.75m, credit.Amount);
        Assert.Equal(975.75m, credit.SignedAmount);

        // camt never expresses direction with a negative amount.
        Assert.All(statement.Entries, entry => Assert.True(entry.Amount >= 0m));
    }

    [Fact]
    public void Parse_ReadsCurrencyFromTheAmountAttribute()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        Assert.All(statement.Entries, entry => Assert.Equal("CHF", entry.Currency));
    }

    [Fact]
    public void Parse_FallsBackToAccountCurrency_WhenTheAmountHasNoCcyAttribute()
    {
        var statement = ParseSingleStatement(CamtFixture.CurrencyFallback);

        Assert.Equal("EUR", statement.Currency);

        var withAttribute = statement.Entries.Single(x => x.AccountServicerReference == "EUR-0001");
        var withoutAttribute = statement.Entries.Single(x => x.AccountServicerReference == "EUR-0002");

        Assert.Equal("EUR", withAttribute.Currency);
        Assert.Equal("EUR", withoutAttribute.Currency);
        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_FlattensBankTransactionCode()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        Assert.Equal("PMNT-CCRD-POSD", statement.Entries[0].BankTransactionCode);
        Assert.Equal("PMNT-RCDT-ESCT", statement.Entries[1].BankTransactionCode);
    }

    [Fact]
    public void Parse_ReadsQrReference_FromProprietaryReferenceType()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        var detail = Assert.Single(statement.Entries[1].Details);

        Assert.Equal("QRR", detail.CreditorReferenceType);
        Assert.NotNull(detail.CreditorReference);
        Assert.Equal("210000000003139471430009017", detail.CreditorReference);
        Assert.Equal(27, detail.CreditorReference.Length);
        Assert.Equal("SALARY-2026-01", detail.EndToEndId);
        Assert.Equal("Example Employer AG", detail.DebtorName);
        Assert.Equal("CH5604835012345678009", detail.DebtorIban);
    }

    [Fact]
    public void Parse_ReadsIsoCreditorReference_FromCodeReferenceType()
    {
        var statement = ParseSingleStatement(CamtFixture.References);

        var scor = Assert.Single(
            statement.Entries.Single(x => x.AccountServicerReference == "REF-SCOR-0001").Details);

        Assert.Equal("SCOR", scor.CreditorReferenceType);
        Assert.Equal("RF18539007547034", scor.CreditorReference);
        Assert.Equal("Swisscom (Schweiz) AG", scor.CreditorName);
    }

    [Fact]
    public void Parse_ReadsIsrReference_FromProprietaryReferenceType()
    {
        var statement = ParseSingleStatement(CamtFixture.References);

        var isr = Assert.Single(
            statement.Entries.Single(x => x.AccountServicerReference == "REF-ISR-0002").Details);

        Assert.Equal("ISR Reference", isr.CreditorReferenceType);
        Assert.Equal("120000000000234478943216899", isr.CreditorReference);
        Assert.Equal("LSV-MND-77120", isr.MandateId);
    }

    [Fact]
    public void Parse_JoinsUnstructuredRemittanceInformation_WhenThereIsNoStructuredReference()
    {
        var statement = ParseSingleStatement(CamtFixture.References);

        var unstructured = Assert.Single(
            statement.Entries.Single(x => x.AccountServicerReference == "REF-USTRD-0003").Details);

        Assert.Null(unstructured.CreditorReferenceType);
        Assert.Null(unstructured.CreditorReference);
        Assert.Equal("Anteil Geschenk Geburtstag Anna", unstructured.UnstructuredRemittanceInformation);

        // NOTPROVIDED is an ISO placeholder, not a reference.
        Assert.Null(unstructured.EndToEndId);
    }

    [Fact]
    public void Parse_CountsAnInternalBatchBookingOnce()
    {
        var statement = ParseSingleStatement(CamtFixture.BatchBooking);

        var batch = statement.Entries.Single(x => x.AccountServicerReference == "BATCH-2026-01-28-0001");

        Assert.True(batch.IsBatchBooking);
        Assert.Equal(3, batch.Details.Count);

        // The entry amount is the batch total, and it is the only amount that counts.
        Assert.Equal(1500.00m, batch.SignedAmount);
        Assert.Equal(1500.00m, batch.Details.Sum(x => x.SignedAmount ?? 0m));

        // Entry level only: 1'500.00 - 250.00.
        Assert.Equal(1250.00m, statement.EntryNetAmount);

        // Summing across both levels would give 2'750.00 and break the balance check.
        var doubleCounted = statement.Entries.Sum(x => x.SignedAmount)
            + statement.Entries.SelectMany(x => x.Details).Sum(x => x.SignedAmount ?? 0m);

        Assert.Equal(2750.00m, doubleCounted);
        Assert.NotEqual(doubleCounted, statement.EntryNetAmount);

        Assert.True(statement.Reconciliation.IsBalanced);
        Assert.Equal(1000.00m, statement.Reconciliation.OpeningBalance);
        Assert.Equal(2250.00m, statement.Reconciliation.ClosingBalance);
    }

    [Fact]
    public void Parse_AcceptsAnEntryWithNoTransactionDetailsAtAll()
    {
        var statement = ParseSingleStatement(CamtFixture.BatchBooking);

        var standingOrder = statement.Entries.Single(x => x.AccountServicerReference == "SO-2026-01-30-0042");

        Assert.Empty(standingOrder.Details);
        Assert.False(standingOrder.IsBatchBooking);
        Assert.Equal(-250.00m, standingOrder.SignedAmount);
        Assert.Equal("Dauerauftrag Sparen", standingOrder.AdditionalEntryInformation);
        Assert.Equal(new DateOnly(2026, 1, 30), standingOrder.ValueDate);
    }

    [Fact]
    public void Parse_ReconciliationPasses_WhenClosingMinusOpeningEqualsTheEntrySum()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        Assert.True(statement.Reconciliation.IsAvailable);
        Assert.True(statement.Reconciliation.IsBalanced);
        Assert.Equal(0m, statement.Reconciliation.Difference);
        Assert.Equal(935.75m, statement.Reconciliation.EntryNetAmount);
        Assert.Equal(4978.37m, statement.Reconciliation.ExpectedClosingBalance);
    }

    [Fact]
    public void Parse_ReconciliationFails_WhenTheFileIsTruncated()
    {
        var statement = ParseSingleStatement(CamtFixture.Truncated);

        Assert.True(statement.Reconciliation.IsAvailable);
        Assert.False(statement.Reconciliation.IsBalanced);
        Assert.Equal(975.75m, statement.Reconciliation.Difference);
        Assert.Equal(4002.62m, statement.Reconciliation.ExpectedClosingBalance);
    }

    [Fact]
    public void Parse_TreatsChargesAsInformationOnly()
    {
        var statement = ParseSingleStatement(CamtFixture.Charges);

        var entry = Assert.Single(statement.Entries);

        Assert.Equal(5.00m, entry.TotalCharges);
        Assert.Equal("CHF", entry.ChargesCurrency);

        // The booked amount already includes the charge; adjusting for it breaks the balance.
        Assert.Equal(-1005.00m, entry.SignedAmount);
        Assert.True(statement.Reconciliation.IsBalanced);
        Assert.Equal(995.00m, statement.Reconciliation.ClosingBalance);

        var detail = Assert.Single(entry.Details);

        Assert.Equal(5.00m, detail.TotalCharges);
    }

    [Fact]
    public void Parse_ReturnsOneStatementPerAccount_ForACombinedFile()
    {
        var file = Parse(CamtFixture.MultiAccount);

        Assert.Equal(3, file.Statements.Count);

        Assert.Equal(
            new[]
            {
                "CH2100210210108311400",
                "CH5604835012345678009",
                "CH9300762011623852957"
            },
            file.Statements.Select(x => x.Iban).ToArray());

        Assert.Equal(
            new[] { "CHF", "CHF", "EUR" },
            file.Statements.Select(x => x.Currency).ToArray());

        // Every statement reconciles against its OWN balances. A file-level sum would be
        // meaningless - it would add CHF and EUR together.
        Assert.All(file.Statements, statement => Assert.True(statement.Reconciliation.IsBalanced));

        // Entries never leak between statements.
        Assert.All(file.Statements, statement => Assert.Single(statement.Entries));

        Assert.Equal("ACCT-A-0001", file.Statements[0].Entries[0].AccountServicerReference);
        Assert.Equal("ACCT-B-0001", file.Statements[1].Entries[0].AccountServicerReference);
        Assert.Equal("ACCT-C-0001", file.Statements[2].Entries[0].AccountServicerReference);
    }

    [Fact]
    public void Parse_ReadsAnOverdrawnClosingBalanceAsNegative()
    {
        var file = Parse(CamtFixture.MultiAccount);

        var overdrawn = file.Statements[1];

        Assert.NotNull(overdrawn.ClosingBalance);
        Assert.Equal(Camt053CreditDebitIndicator.Debit, overdrawn.ClosingBalance.CreditDebitIndicator);
        Assert.Equal(250.00m, overdrawn.ClosingBalance.Amount);
        Assert.Equal(-250.00m, overdrawn.ClosingBalance.SignedAmount);
        Assert.True(overdrawn.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_FallsBackToPreviousClosingBalance_WhenThereIsNoOpeningBalance()
    {
        var file = Parse(CamtFixture.MultiAccount);

        var withPrcd = file.Statements[2];

        Assert.NotNull(withPrcd.OpeningBalance);
        Assert.Equal("PRCD", withPrcd.OpeningBalance.TypeCode);
        Assert.Equal(400.00m, withPrcd.OpeningBalance.SignedAmount);
        Assert.True(withPrcd.Reconciliation.IsBalanced);
    }

    /// <summary>
    /// The whole point of matching on local names: <c>.04</c> and <c>.08</c> differ in
    /// namespace, in the <c>BIC</c>/<c>BICFI</c> element, in how <c>Sts</c> is wrapped, in
    /// whether the party sits under a <c>Pty</c> choice and in where the detail amount lives -
    /// and none of that needs a second code path.
    /// </summary>
    [Theory]
    [InlineData(CamtFixture.Plain08, "08")]
    [InlineData(CamtFixture.Plain04, "04")]
    public void Parse_ProducesTheSameStatement_ForBothSchemaRevisions(
        string fixtureName,
        string expectedSchemaVersion)
    {
        var file = Parse(fixtureName);

        Assert.Equal(expectedSchemaVersion, file.SchemaVersion);

        var statement = Assert.Single(file.Statements);

        Assert.Equal("CH2100210210108311400", statement.Iban);
        Assert.Equal("CHF", statement.Currency);
        Assert.Equal("UBSWCHZH80A", statement.ServicerBic);
        Assert.Equal(4042.62m, statement.OpeningBalance?.SignedAmount);
        Assert.Equal(4978.37m, statement.ClosingBalance?.SignedAmount);
        Assert.True(statement.Reconciliation.IsBalanced);

        Assert.Equal(2, statement.Entries.Count);

        var debit = statement.Entries[0];

        Assert.Equal("9910005GK0615030", debit.AccountServicerReference);
        Assert.Equal(-40.00m, debit.SignedAmount);
        Assert.Equal("PMNT-CCRD-POSD", debit.BankTransactionCode);
        Assert.Equal("BOOK", debit.Status);
        Assert.Equal(new DateOnly(2026, 1, 5), debit.ValueDate);
        Assert.Equal(
            "BAZG VIA-WEBSHOP",
            Assert.Single(debit.Details).CreditorName);
        Assert.Equal(
            "Zahlung UBS TWINT",
            Assert.Single(debit.Details).UnstructuredRemittanceInformation);
        Assert.Equal(40.00m, Assert.Single(debit.Details).Amount);

        var credit = statement.Entries[1];

        Assert.Equal("9999023ZC7856428", credit.AccountServicerReference);
        Assert.Equal(975.75m, credit.SignedAmount);
        Assert.Equal("PMNT-RCDT-ESCT", credit.BankTransactionCode);
        Assert.Equal("BOOK", credit.Status);
        Assert.Equal(new DateOnly(2026, 1, 23), credit.ValueDate);
        Assert.Equal("QRR", Assert.Single(credit.Details).CreditorReferenceType);
        Assert.Equal(
            "210000000003139471430009017",
            Assert.Single(credit.Details).CreditorReference);
        Assert.Equal("Example Employer AG", Assert.Single(credit.Details).DebtorName);
    }

    [Fact]
    public void Parse_KeepsTheRawEntryXmlForDiagnostics()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        Assert.StartsWith("<Ntry", statement.Entries[0].RawXml, StringComparison.Ordinal);
        Assert.Contains("9910005GK0615030", statement.Entries[0].RawXml, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_ReadsReversalIndicatorWithoutFlippingTheSign()
    {
        var statement = ParseSingleStatement(CamtFixture.Plain08);

        // ISO already carries the direction of a reversal in CdtDbtInd; flipping again would
        // double-negate.
        Assert.All(statement.Entries, entry => Assert.False(entry.IsReversal));
        Assert.Equal(-40.00m, statement.Entries[0].SignedAmount);
    }

    [Fact]
    public void Parse_Throws_WhenTheXmlIsMalformed()
    {
        var parser = new Camt053Parser();

        var exception = Assert.Throws<Camt053ParseException>(() =>
            parser.Parse(CamtFixture.ReadBytes(CamtFixture.Malformed)));

        Assert.Contains("not valid XML", exception.Message, StringComparison.Ordinal);
        Assert.Contains("line", exception.Message, StringComparison.Ordinal);
        Assert.IsType<System.Xml.XmlException>(exception.InnerException);
    }

    [Fact]
    public void Parse_Throws_WhenTheDocumentIsNotABankToCustomerStatement()
    {
        var parser = new Camt053Parser();

        var exception = Assert.Throws<Camt053ParseException>(() =>
            parser.Parse(CamtFixture.ReadBytes(CamtFixture.NotCamt)));

        Assert.Contains("BkToCstmrStmt", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenTheContentIsEmpty()
    {
        var parser = new Camt053Parser();

        Assert.Throws<Camt053ParseException>(() => parser.Parse([]));
        Assert.Throws<Camt053ParseException>(() => parser.Parse("   "));
    }

    [Fact]
    public void Parse_Throws_WhenAnAmountIsNegative()
    {
        var parser = new Camt053Parser();

        var text = CamtFixture.ReadText(CamtFixture.Plain08)
            .Replace(
                "<Amt Ccy=\"CHF\">40.00</Amt>",
                "<Amt Ccy=\"CHF\">-40.00</Amt>",
                StringComparison.Ordinal);

        var exception = Assert.Throws<Camt053ParseException>(() => parser.Parse(text));

        Assert.Contains("negative", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_Throws_WhenCreditDebitIndicatorIsUnsupported()
    {
        var parser = new Camt053Parser();

        var text = CamtFixture.ReadText(CamtFixture.Plain08)
            .Replace(
                "<CdtDbtInd>DBIT</CdtDbtInd>",
                "<CdtDbtInd>MAYBE</CdtDbtInd>",
                StringComparison.Ordinal);

        var exception = Assert.Throws<Camt053ParseException>(() => parser.Parse(text));

        Assert.Contains("MAYBE", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Parse_IgnoresAnUnknownNamespace()
    {
        // A future .09 or .10 revision must parse through the same code path.
        var text = CamtFixture.ReadText(CamtFixture.Plain08)
            .Replace(
                "camt.053.001.08",
                "camt.053.001.09",
                StringComparison.Ordinal);

        var file = new Camt053Parser().Parse(text);

        Assert.Equal("09", file.SchemaVersion);
        Assert.Equal(2, Assert.Single(file.Statements).Entries.Count);
    }

    [Fact]
    public void Parse_RejectsDocumentTypeDefinitions()
    {
        // A bank statement is untrusted input; a DTD is an entity-expansion vector and an
        // external-entity fetch, which would also break the app's no-network guarantee.
        const string WithDtd =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <!DOCTYPE Document [<!ENTITY x "expanded">]>
            <Document xmlns="urn:iso:std:iso:20022:tech:xsd:camt.053.001.08">
              <BkToCstmrStmt />
            </Document>
            """;

        var exception = Assert.Throws<Camt053ParseException>(() =>
            new Camt053Parser().Parse(WithDtd));

        Assert.Contains("not valid XML", exception.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CamtFixture.Plain08, true)]
    [InlineData(CamtFixture.Plain04, true)]
    [InlineData(CamtFixture.NotCamt, false)]
    public void LooksLikeCamt053_DetectsTheFormatFromTheContent(
        string fixtureName,
        bool expected)
    {
        Assert.Equal(
            expected,
            Camt053Parser.LooksLikeCamt053(CamtFixture.ReadText(fixtureName)));
    }

    [Fact]
    public void LooksLikeCamt053_RejectsMt940()
    {
        const string Mt940 =
            """
            :20:02100010831101
            :25:CH230021021010831140E
            :28C:142/1
            :60F:C260101CHF4042,62
            :62F:C261231CHF4978,37
            """;

        Assert.False(Camt053Parser.LooksLikeCamt053(Mt940));
        Assert.False(Camt053Parser.LooksLikeCamt053(string.Empty));
    }
}
