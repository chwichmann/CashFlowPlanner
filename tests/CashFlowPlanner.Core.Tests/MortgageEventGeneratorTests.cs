using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

public sealed class MortgageEventGeneratorTests
{
    [Fact]
    public void Generate_Should_CreateQuarterlyInterestAndDirectAmortisationEvents()
    {
        // Arrange
        var mortgage = CreateSaronMortgage(
            amortisationMode: AmortisationMode.Direct);

        var generator = new MortgageEventGenerator();

        // Act
        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        var events = generation.Events;

        // Assert
        Assert.Equal(2, events.Count);

        var interest = events.Single(x => x.Name.EndsWith("interest"));
        var amortisation = events.Single(x => x.Name.EndsWith("amortisation"));

        Assert.Equal(new DateOnly(2026, 3, 31), interest.Date);
        Assert.Equal(TransactionKind.ExternalExpense, interest.Kind);
        Assert.Equal(mortgage.PaymentAccountId, interest.FromAccountId);
        Assert.Null(interest.ToAccountId);
        Assert.True(interest.Amount > 0);

        Assert.Equal(new DateOnly(2026, 3, 31), amortisation.Date);
        Assert.Equal(TransactionKind.ExternalExpense, amortisation.Kind);
        Assert.Equal(mortgage.PaymentAccountId, amortisation.FromAccountId);
        Assert.Null(amortisation.ToAccountId);
        Assert.Equal(2250m, amortisation.Amount);

        var endPrincipal = generation.PrincipalPoints
            .Where(x => x.MortgageId == mortgage.Id)
            .OrderByDescending(x => x.Date)
            .First()
            .Principal;

        Assert.Equal(702750m, endPrincipal);
    }

    [Fact]
    public void Generate_Should_UseLastBusinessDayBeforeNextQuarter()
    {
        // Arrange
        var mortgage = CreateFixedMortgage();
        var generator = new MortgageEventGenerator();

        // Act
        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var events = generation.Events;

        // Assert
        var paymentDates = events
            .Select(x => x.Date)
            .Distinct()
            .ToList();

        Assert.Equal(
        [
            new DateOnly(2026, 3, 31),
            new DateOnly(2026, 6, 30),
            new DateOnly(2026, 9, 30),
            new DateOnly(2026, 12, 31)
        ], paymentDates);
    }

    [Fact]
    public void CalculateInterestForPeriod_Should_UseFixedInterest_ForFixedMortgage()
    {
        // Arrange
        var mortgage = CreateFixedMortgage(
            fixedInterestPercent: 1.0m,
            calculationPrincipal: 365000m);

        // Act
        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            365000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2));

        // Assert
        // 365000 * 1% / 365 = 10
        Assert.Equal(10m, interest);
    }

    [Fact]
    public void CalculateInterestForPeriod_Should_AddPositiveSaron_ToFixedInterest()
    {
        // Arrange
        var mortgage = CreateSaronMortgage(
            fixedInterestPercent: 0.65m,
            saronRatePercent: 1.35m,
            calculationPrincipal: 365000m);

        // Act
        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            365000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2));

        // Assert
        // Effective rate = 2.0%
        // 365000 * 2% / 365 = 20
        Assert.Equal(20m, interest);
    }

    [Fact]
    public void CalculateInterestForPeriod_Should_NotApplyNegativeSaron()
    {
        // Arrange
        var mortgage = CreateSaronMortgage(
            fixedInterestPercent: 0.65m,
            saronRatePercent: -1.00m,
            calculationPrincipal: 365000m);

        // Act
        var interest = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            365000m,
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 1, 2));

        // Assert
        // Effective rate = 0.65%, not -0.35%
        var expected = Math.Round(
            365000m * (0.65m / 100m) / 365m,
            2,
            MidpointRounding.AwayFromZero);

        Assert.Equal(expected, interest);
    }

    [Fact]
    public void Generate_Should_CreateIndirectAmortisationTransfer_WithoutDebtPayment()
    {
        // Arrange
        var mortgage = CreateSaronMortgage(
            amortisationMode: AmortisationMode.Indirect);

        var generator = new MortgageEventGenerator();

        // Act
        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        var events = generation.Events;

        // Assert
        Assert.Equal(2, events.Count);

        var indirectAmortisation = events.Single(x => x.Name.EndsWith("indirect amortisation"));

        Assert.Equal(TransactionKind.InternalTransfer, indirectAmortisation.Kind);
        Assert.Equal(mortgage.PaymentAccountId, indirectAmortisation.FromAccountId);
        Assert.Equal(mortgage.IndirectAmortisationAccountId, indirectAmortisation.ToAccountId);
        Assert.Equal(2250m, indirectAmortisation.Amount);

        Assert.DoesNotContain(events, x => x.Kind == TransactionKind.DebtPayment);

        var endPrincipal = generation.PrincipalPoints
            .Where(x => x.MortgageId == mortgage.Id)
            .OrderByDescending(x => x.Date)
            .First()
            .Principal;

        Assert.Equal(705000m, endPrincipal);
    }

    [Fact]
    public void Generate_Should_NotCreateAmortisation_WhenModeIsNone()
    {
        // Arrange
        var mortgage = CreateSaronMortgage(
            amortisationMode: AmortisationMode.None);

        var generator = new MortgageEventGenerator();

        // Act
        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 3, 31));

        var events = generation.Events;

        // Assert
        Assert.Single(events);
        Assert.Equal(TransactionKind.ExternalExpense, events[0].Kind);
        Assert.EndsWith("interest", events[0].Name);

        var endPrincipal = generation.PrincipalPoints
            .Where(x => x.MortgageId == mortgage.Id)
            .OrderByDescending(x => x.Date)
            .First()
            .Principal;

        Assert.Equal(705000m, endPrincipal);
    }

    [Fact]
    public void Generate_WhenSimulationStartsInsideBankQuarter_FirstInterestUsesWholeNaturalQuarter()
    {
        var paymentAccountId = Guid.NewGuid();

        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "Test SARON Mortgage",
            Type = MortgageType.Saron,
            PaymentAccountId = paymentAccountId,
            InitialPrincipal = 300_000m,
            InitialDate = new DateOnly(2020, 1, 1),
            CalculationPrincipal = 300_000m,
            CalculationPrincipalDate = new DateOnly(2026, 5, 15),
            FixedInterestPercent = 0.65m,
            SaronRates =
            [
                new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 4, 1),
                RatePercent = 0.70m
            }
            ],
            AmortisationMode = AmortisationMode.None,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };

        var generator = new MortgageEventGenerator();

        var result = generator.Generate(
            [mortgage],
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 6, 30));

        var interestEvent = Assert.Single(
            result.Events,
            x => x.Category == "Mortgage Interest");

        Assert.Equal(new DateOnly(2026, 6, 30), interestEvent.Date);

        var expectedFullQuarter = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            300_000m,
            new DateOnly(2026, 4, 1),
            new DateOnly(2026, 7, 1));

        var truncatedAmount = MortgageEventGenerator.CalculateInterestForPeriod(
            mortgage,
            300_000m,
            new DateOnly(2026, 5, 15),
            new DateOnly(2026, 7, 1));

        Assert.Equal(expectedFullQuarter, interestEvent.Amount);
        Assert.True(interestEvent.Amount > truncatedAmount);
    }

    private static MortgageContract CreateFixedMortgage(
        decimal fixedInterestPercent = 1.0m,
        decimal calculationPrincipal = 705000m)
    {
        return new MortgageContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "Fixed Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            InitialPrincipal = 750000m,
            InitialDate = new DateOnly(2021, 8, 1),
            CalculationPrincipal = calculationPrincipal,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = fixedInterestPercent,
            AmortisationMode = AmortisationMode.Direct,
            AnnualAmortisationAmount = 9000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };
    }

    private static MortgageContract CreateSaronMortgage(
        AmortisationMode amortisationMode = AmortisationMode.Direct,
        decimal fixedInterestPercent = 0.65m,
        decimal saronRatePercent = 1.20m,
        decimal calculationPrincipal = 705000m)
    {
        return new MortgageContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "SARON Mortgage",
            Type = MortgageType.Saron,
            PaymentAccountId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
            IndirectAmortisationAccountId = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            InitialPrincipal = 750000m,
            InitialDate = new DateOnly(2021, 8, 1),
            CalculationPrincipal = calculationPrincipal,
            CalculationPrincipalDate = new DateOnly(2026, 1, 1),
            FixedInterestPercent = fixedInterestPercent,
            SaronRates =
            [
                new MortgageInterestRatePoint
                {
                    Date = new DateOnly(2026, 1, 1),
                    RatePercent = saronRatePercent
                }
            ],
            AmortisationMode = amortisationMode,
            AnnualAmortisationAmount = 9000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };
    }
}