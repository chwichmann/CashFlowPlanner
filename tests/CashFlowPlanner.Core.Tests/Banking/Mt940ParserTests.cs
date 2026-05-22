using System.Text;
using CashFlowPlanner.Core.Banking.Mt940;

namespace CashFlowPlanner.Core.Tests.Banking.Mt940;

public sealed class Mt940ParserTests
{
    [Fact]
    public void Parse_LightStatement_ParsesHeaderBalancesTransactionsAndReconciliation()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(CreateLightStatement());

        Assert.Equal("02100010943001", statement.TransactionReference);
        Assert.Equal("CH9800210210109430M1A", statement.AccountIdentifier);
        Assert.Equal("142/1", statement.StatementNumber);

        Assert.NotNull(statement.OpeningBalance);
        Assert.Equal(new DateOnly(2026, 1, 1), statement.OpeningBalance.Date);
        Assert.Equal(1004.25m, statement.OpeningBalance.Amount);
        Assert.Equal("CHF", statement.OpeningBalance.Currency);

        Assert.NotNull(statement.ClosingBalance);
        Assert.Equal(new DateOnly(2026, 12, 31), statement.ClosingBalance.Date);
        Assert.Equal(1084.25m, statement.ClosingBalance.Amount);
        Assert.Equal("CHF", statement.ClosingBalance.Currency);

        Assert.Equal(4, statement.Transactions.Count);

        Assert.True(statement.Reconciliation.IsAvailable);
        Assert.True(statement.Reconciliation.IsBalanced);
        Assert.Equal(80m, statement.Reconciliation.TransactionNetAmount);
        Assert.Equal(1084.25m, statement.Reconciliation.ExpectedClosingBalance);
        Assert.Equal(0m, statement.Reconciliation.Difference);
    }

    [Fact]
    public void Parse_EnrichedStatement_ExtractsBlock4AndParsesBody()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(CreateEnrichedStatement());

        Assert.Equal("02100010943001", statement.TransactionReference);
        Assert.Equal("CH9800210210109430M1A", statement.AccountIdentifier);
        Assert.Equal(4, statement.Transactions.Count);

        Assert.DoesNotContain("{1:", statement.RawBody);
        Assert.DoesNotContain("{5:", statement.RawBody);

        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_Transaction_ParsesDebitAsNegativeAmount()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601050105D40,NMSCNONREF//9910005GK0615030
            Zahlung UBS TWINT
            :86:K70?TEST MERCHANT Zahlung UBS TWINT
            :62F:C260105CHF960,00
            """);

        var transaction = Assert.Single(statement.Transactions);

        Assert.Equal(new DateOnly(2026, 1, 5), transaction.ValueDate);
        Assert.Equal(new DateOnly(2026, 1, 5), transaction.BookingDate);
        Assert.Equal(Mt940DebitCreditIndicator.Debit, transaction.DebitCreditIndicator);
        Assert.Equal(-40m, transaction.SignedAmount);
        Assert.Equal(40m, transaction.Amount);
        Assert.Equal("NMSC", transaction.TransactionCode);
        Assert.Equal("NONREF", transaction.CustomerReference);
        Assert.Equal("9910005GK0615030", transaction.BankReference);
        Assert.Equal("K70", transaction.Structured86Code);
        Assert.Contains("TEST MERCHANT", transaction.Description);

        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_Transaction_ParsesCreditAsPositiveAmount()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601230123C9958,8NTRFNONREF//9999023ZC7856428
            Salary
            :86:Z32?Example Employer Salary
            :62F:C260123CHF10958,80
            """);

        var transaction = Assert.Single(statement.Transactions);

        Assert.Equal(Mt940DebitCreditIndicator.Credit, transaction.DebitCreditIndicator);
        Assert.Equal(9958.8m, transaction.SignedAmount);
        Assert.Equal("NTRF", transaction.TransactionCode);
        Assert.Equal("Z32", transaction.Structured86Code);
        Assert.Equal("9999023ZC7856428", transaction.BankReference);

        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_Bytes_FallsBackToLatin1_WhenInputIsNotUtf8()
    {
        var parser = new Mt940Parser();

        var text =
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601060107D9,6NTRFNONREF//9930507BN9204976
            Zahlung Debitkarte
            :86:DE1?Frei's Brötli-Bar Zahlung Debitkarte
            :62F:C260107CHF990,40
            """;

        var bytes = Encoding.Latin1.GetBytes(text);

        var statement = parser.Parse(bytes);

        var transaction = Assert.Single(statement.Transactions);

        Assert.Contains("Brötli-Bar", transaction.Description);
        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_StatementWithMultipleTransactions_ParsesAllTransactions()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601050105D40,NMSCNONREF//REF1
            Payment 1
            :86:K70?Merchant 1
            :61:2601060106D10,NTRFNONREF//REF2
            Payment 2
            :86:Z44?Merchant 2
            :61:2601070107C100,NTRFNONREF//REF3
            Incoming
            :86:Z04?Refund
            :62F:C260107CHF1050,00
            """);

        Assert.Equal(3, statement.Transactions.Count);
        Assert.Equal(-40m, statement.Transactions[0].SignedAmount);
        Assert.Equal(-10m, statement.Transactions[1].SignedAmount);
        Assert.Equal(100m, statement.Transactions[2].SignedAmount);
        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_UnbalancedStatement_ReturnsReconciliationDifference()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601050105D40,NMSCNONREF//REF1
            Payment
            :86:K70?Merchant
            :62F:C260105CHF970,00
            """);

        Assert.True(statement.Reconciliation.IsAvailable);
        Assert.False(statement.Reconciliation.IsBalanced);
        Assert.Equal(960m, statement.Reconciliation.ExpectedClosingBalance);
        Assert.Equal(10m, statement.Reconciliation.Difference);
    }

    [Fact]
    public void Parse_ReversalOfCredit_IsNegative()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601050105RC40,NTRFNONREF//REF1
            Reversal
            :86:Z04?Reversal of credit
            :62F:C260105CHF960,00
            """);

        var transaction = Assert.Single(statement.Transactions);

        Assert.Equal(Mt940DebitCreditIndicator.ReversalOfCredit, transaction.DebitCreditIndicator);
        Assert.Equal(-40m, transaction.SignedAmount);
        Assert.True(statement.Reconciliation.IsBalanced);
    }

    [Fact]
    public void Parse_ReversalOfDebit_IsPositive()
    {
        var parser = new Mt940Parser();

        var statement = parser.Parse(
            """
            :20:TEST
            :25:CH230021021010831140E
            :28C:1/1
            :60F:C260101CHF1000,00
            :61:2601050105RD40,NTRFNONREF//REF1
            Reversal
            :86:Z04?Reversal of debit
            :62F:C260105CHF1040,00
            """);

        var transaction = Assert.Single(statement.Transactions);

        Assert.Equal(Mt940DebitCreditIndicator.ReversalOfDebit, transaction.DebitCreditIndicator);
        Assert.Equal(40m, transaction.SignedAmount);
        Assert.True(statement.Reconciliation.IsBalanced);
    }

    private static string CreateLightStatement()
    {
        return
            """
            :20:02100010943001
            :25:CH9800210210109430M1A
            :28C:142/1
            :60F:C260101CHF1004,25
            :61:2601230123C20,NTRFNONREF//9910523LK1315518
            Credit
            :86:Z04?Example Person Credit
            :61:2602250225C20,NTRFNONREF//9910556LK4218937
            Credit
            :86:Z04?Example Person Credit
            :61:2603260326C20,NTRFNONREF//9910585LK7465960
            Credit
            :86:Z04?Example Person Credit
            :61:2604240424C20,NTRFNONREF//9910114LK0354851
            Credit
            :86:Z04?Example Person Credit
            :62F:C261231CHF1084,25
            """;
    }

    private static string CreateEnrichedStatement()
    {
        return
            """
            {1:F01UBSWCHZHX80A0000000000}{2:I940X N}{3:{108:260101/261231}}{4:
            :20:02100010943001
            :25:CH9800210210109430M1A
            :28C:142/1
            :60F:C260101CHF1004,25
            :61:2601230123C20,NTRFNONREF//9910523LK1315518
            Credit
            :86:Z04?Example Person Credit
            :61:2602250225C20,NTRFNONREF//9910556LK4218937
            Credit
            :86:Z04?Example Person Credit
            :61:2603260326C20,NTRFNONREF//9910585LK7465960
            Credit
            :86:Z04?Example Person Credit
            :61:2604240424C20,NTRFNONREF//9910114LK0354851
            Credit
            :86:Z04?Example Person Credit
            :62F:C261231CHF1084,25
            -}{5:{CHK:000000000000}}
            """;
    }
}