using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

/// <summary>
/// H1 and H2: <see cref="MortgageContract.CalculationPrincipalDate"/> is the date
/// on which the principal is KNOWN, not the date the mortgage starts existing.
///
/// The generator used to anchor everything on that date:
/// <c>Max(simulationStart, calculationPrincipalDate)</c>. Both directions were
/// wrong.
///
/// H1, date in the future: the whole stretch between the simulation start and
/// that date had no principal point and no billing periods, so the mortgage read
/// as zero debt and its interest was never charged.
///
/// H2, date in the past: the known principal was used verbatim at the simulation
/// start, ignoring every amortisation instalment paid in between, so all later
/// interest was computed on an inflated base.
///
/// The principal is now rolled along the billing calendar to the simulation
/// start, in whichever direction is needed.
/// </summary>
public sealed class MortgageCalculationPrincipalDateTests
{
    private static readonly Guid PaymentAccountId =
        Guid.Parse("10000000-0000-0000-0000-000000000002");

    [Fact]
    public void Generate_CalculationPrincipalDateInFuture_ChargesInterestFromSimulationStart()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 7, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var interestDates = generation.Events
            .Where(x => x.Category == "Mortgage Interest")
            .Select(x => x.Date)
            .ToList();

        // All four bank quarters, not just the two after 01.07.
        Assert.Equal(
            [
                new DateOnly(2026, 3, 31),
                new DateOnly(2026, 6, 30),
                new DateOnly(2026, 9, 30),
                new DateOnly(2026, 12, 31)
            ],
            interestDates);
    }

    [Fact]
    public void Generate_CalculationPrincipalDateInFuture_SeedsPrincipalAtSimulationStart()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 7, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var firstPoint = generation.PrincipalPoints
            .OrderBy(x => x.Date)
            .First();

        // Two quarterly instalments of 2'250 are still owed between 01.01 and
        // 01.07, so on 01.01 the debt was 705'000 + 4'500.
        Assert.Equal(new DateOnly(2026, 1, 1), firstPoint.Date);
        Assert.Equal(709_500m, firstPoint.Principal);
    }

    /// <summary>
    /// The defining property of the backward roll: whatever the user typed as
    /// "known principal on this date" must still be the principal in force on
    /// that date once the engine has generated its way there from the simulation
    /// start.
    /// </summary>
    [Theory]
    [InlineData(2026, 4, 1)]
    [InlineData(2026, 7, 1)]
    [InlineData(2026, 10, 1)]
    public void Generate_PrincipalInForceOnCalculationPrincipalDate_IsTheCalculationPrincipal(
        int year,
        int month,
        int day)
    {
        var calculationPrincipalDate = new DateOnly(year, month, day);

        var mortgage = CreateMortgage(calculationPrincipalDate);

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        // "In force on D" = after every instalment with a payment date before D.
        var inForce = generation.PrincipalPoints
            .Where(x => x.Date < calculationPrincipalDate)
            .OrderByDescending(x => x.Date)
            .First()
            .Principal;

        Assert.Equal(705_000m, inForce);
    }

    [Fact]
    public void Generate_CalculationPrincipalDateInFuture_EmitsWarning()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 7, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var warning = Assert.Single(generation.Warnings);

        Assert.Equal("MORTGAGE_PRINCIPAL_DATE_IN_FUTURE", warning.Code);
        Assert.Equal(WarningSeverity.Warning, warning.Severity);
        Assert.Equal(mortgage.Id, warning.SourceId);
    }

    /// <summary>
    /// H2: 705'000 known on 01.01.2021, 9'000 a year of direct amortisation,
    /// simulated from 2026. Twenty quarterly instalments of 2'250 were paid in
    /// between -- CHF 45'000 the engine used to ignore.
    /// </summary>
    [Fact]
    public void Generate_CalculationPrincipalDateInPast_AmortisesForwardToSimulationStart()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2021, 1, 1),
            initialDate: new DateOnly(2020, 1, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var firstPoint = generation.PrincipalPoints
            .OrderBy(x => x.Date)
            .First();

        Assert.Equal(new DateOnly(2026, 1, 1), firstPoint.Date);
        Assert.Equal(660_000m, firstPoint.Principal);

        var warning = Assert.Single(generation.Warnings);
        Assert.Equal("MORTGAGE_PRINCIPAL_ROLLED_FORWARD", warning.Code);
        Assert.Equal(mortgage.Id, warning.SourceId);
    }

    [Fact]
    public void Generate_CalculationPrincipalDateOnSimulationStart_EmitsNoWarningAndDoesNotRoll()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 1, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Empty(generation.Warnings);

        Assert.Equal(
            705_000m,
            generation.PrincipalPoints.OrderBy(x => x.Date).First().Principal);
    }

    /// <summary>
    /// Indirect and no amortisation never reduce the principal, so rolling it is
    /// the identity and must not invent a warning either.
    /// </summary>
    [Theory]
    [InlineData(AmortisationMode.Indirect)]
    [InlineData(AmortisationMode.None)]
    public void Generate_WithoutDirectAmortisation_PrincipalIsNotRolled(
        AmortisationMode amortisationMode)
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2021, 1, 1),
            initialDate: new DateOnly(2020, 1, 1),
            amortisationMode: amortisationMode);

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        Assert.Equal(
            705_000m,
            generation.PrincipalPoints.OrderBy(x => x.Date).First().Principal);

        Assert.Empty(generation.Warnings);
    }

    /// <summary>
    /// A mortgage that only starts inside the simulated range must not be
    /// reported before it exists, and its principal must not be rolled back past
    /// its own initial date.
    /// </summary>
    [Fact]
    public void Generate_MortgageStartingInsideRange_AnchorsOnItsInitialDate()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 7, 1),
            initialDate: new DateOnly(2026, 7, 1));

        var generator = new MortgageEventGenerator();

        var generation = generator.Generate(
            [mortgage],
            new DateOnly(2026, 1, 1),
            new DateOnly(2026, 12, 31));

        var firstPoint = generation.PrincipalPoints
            .OrderBy(x => x.Date)
            .First();

        Assert.Equal(new DateOnly(2026, 7, 1), firstPoint.Date);
        Assert.Equal(705_000m, firstPoint.Principal);

        Assert.Empty(generation.Warnings);
    }

    /// <summary>
    /// H1 as the user sees it: the report used to show zero debt for every date
    /// before the calculation principal date.
    /// </summary>
    [Fact]
    public void Simulate_CalculationPrincipalDateInFuture_DoesNotReportZeroDebt()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2026, 7, 1));

        var plan = CreatePlan(mortgage);

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(
            709_500m,
            result.GetMortgagePrincipal(mortgage.Id, new DateOnly(2026, 3, 1)));

        Assert.Contains(
            result.Warnings,
            x => x.Code == "MORTGAGE_PRINCIPAL_DATE_IN_FUTURE");
    }

    /// <summary>
    /// The other half of H1: even with the principal points fixed, a lookup that
    /// lands before the first point must not answer "no debt".
    /// </summary>
    [Fact]
    public void TryGetMortgagePrincipal_BeforeFirstPoint_ReturnsEarliestKnownPointNotZero()
    {
        var mortgageId = Guid.NewGuid();

        var result = new SimulationResult
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Events = [],
            BalancePoints = [],
            Warnings = [],
            MortgagePrincipalPoints =
            [
                new MortgagePrincipalPoint
                {
                    Date = new DateOnly(2026, 7, 1),
                    MortgageId = mortgageId,
                    MortgageName = "House",
                    Principal = 705_000m
                }
            ]
        };

        Assert.Equal(705_000m, result.TryGetMortgagePrincipal(mortgageId, new DateOnly(2026, 3, 1)));
        Assert.Equal(705_000m, result.GetMortgagePrincipal(mortgageId, new DateOnly(2026, 3, 1)));
    }

    [Fact]
    public void TryGetMortgagePrincipal_UnknownMortgage_ReturnsNull()
    {
        var result = new SimulationResult
        {
            StartDate = new DateOnly(2026, 1, 1),
            EndDate = new DateOnly(2026, 12, 31),
            Events = [],
            BalancePoints = [],
            Warnings = [],
            MortgagePrincipalPoints = []
        };

        Assert.Null(result.TryGetMortgagePrincipal(Guid.NewGuid(), new DateOnly(2026, 3, 1)));
        Assert.Equal(0m, result.GetMortgagePrincipal(Guid.NewGuid(), new DateOnly(2026, 3, 1)));
    }

    /// <summary>
    /// Validate() deliberately still accepts a calculation principal date after
    /// the simulation start. The contract cannot see the simulation range, that
    /// range moves on its own (a rolling horizon re-anchors every day), and
    /// CashFlowPlan.Validate() gates saving and exporting -- rejecting it there
    /// would turn a legitimate "my statement of 1 July says 705'000" into a plan
    /// that can no longer be saved. It is a simulation warning instead.
    /// </summary>
    [Fact]
    public void Validate_CalculationPrincipalDateInFuture_IsAccepted()
    {
        var mortgage = CreateMortgage(
            calculationPrincipalDate: new DateOnly(2099, 1, 1));

        mortgage.Validate();

        CreatePlan(mortgage).Validate();
    }

    private static MortgageContract CreateMortgage(
        DateOnly calculationPrincipalDate,
        DateOnly? initialDate = null,
        AmortisationMode amortisationMode = AmortisationMode.Direct)
    {
        return new MortgageContract
        {
            Id = Guid.Parse("30000000-0000-0000-0000-000000000001"),
            Name = "House",
            Type = MortgageType.Fixed,
            PaymentAccountId = PaymentAccountId,
            IndirectAmortisationAccountId = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            InitialPrincipal = 750_000m,
            InitialDate = initialDate ?? new DateOnly(2020, 1, 1),
            CalculationPrincipal = 705_000m,
            CalculationPrincipalDate = calculationPrincipalDate,
            FixedInterestPercent = 1.0m,
            AmortisationMode = amortisationMode,
            AnnualAmortisationAmount = 9_000m,
            PaymentInterval = MortgagePaymentInterval.Quarterly,
            BillingCalendar = MortgageBillingCalendar.BankQuarters,
            IsActive = true
        };
    }

    private static CashFlowPlan CreatePlan(MortgageContract mortgage)
    {
        var paymentAccount = new Account
        {
            Id = PaymentAccountId,
            Name = "Savings Account",
            Type = AccountType.SavingsAccount,
            Currency = "CHF",
            OpeningBalance = 500_000m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        var indirectAccount = new Account
        {
            Id = Guid.Parse("10000000-0000-0000-0000-000000000005"),
            Name = "Pillar 3a",
            Type = AccountType.Investment,
            Currency = "CHF",
            OpeningBalance = 0m,
            OpeningDate = new DateOnly(2026, 1, 1)
        };

        return new CashFlowPlan
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage principal date plan",
            BaseCurrency = "CHF",
            Accounts = [paymentAccount, indirectAccount],
            Mortgages = [mortgage],
            SimulationSettings = new SimulationSettings
            {
                DateMode = SimulationDateMode.ExplicitDateRange,
                StartDate = new DateOnly(2026, 1, 1),
                EndDate = new DateOnly(2026, 12, 31)
            }
        };
    }
}
