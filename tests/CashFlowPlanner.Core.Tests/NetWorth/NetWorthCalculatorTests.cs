using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.CreditCards;
using CashFlowPlanner.Core.Mortgages;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.RealEstate;

namespace CashFlowPlanner.Core.Tests.NetWorth;

public sealed class NetWorthCalculatorTests
{
    private static readonly DateOnly Start = new(2026, 1, 1);
    private static readonly DateOnly End = new(2026, 12, 31);

    /// <summary>
    /// The consolidation the result could not do before: a household with a
    /// mortgage and the flat it paid for read as pure debt, because the two
    /// series had no meeting point.
    /// </summary>
    [Fact]
    public void Simulate_MortgageAndProperty_NetsTheDebtAgainstTheAsset()
    {
        var bank = TestPlanBuilder.CreateBankAccount(openingBalance: 50_000m);

        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "Flat mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = bank.Id,
            InitialPrincipal = 700_000m,
            InitialDate = new DateOnly(2021, 1, 1),
            CalculationPrincipal = 700_000m,
            CalculationPrincipalDate = Start,
            FixedInterestPercent = 1m,
            AmortisationMode = AmortisationMode.None
        };

        var flat = new RealEstateAsset
        {
            Id = Guid.NewGuid(),
            Name = "Flat",
            Type = RealEstateType.Flat,
            CurrentEstimatedValue = 900_000m,
            LinkedMortgageIds = [mortgage.Id]
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bank],
            mortgages: [mortgage],
            realEstateAssets: [flat],
            startDate: Start,
            endDate: End);

        var result = new SimulationEngine().Simulate(plan);

        var opening = result.NetWorthPoints[0];

        Assert.Equal(50_000m, opening.LiquidAssets);
        Assert.Equal(900_000m, opening.RealEstateValue);
        Assert.Equal(700_000m, opening.MortgagePrincipal);
        Assert.Equal(0m, opening.OtherLiabilities);
        Assert.Equal(950_000m, opening.TotalAssets);
        Assert.Equal(700_000m, opening.TotalLiabilities);
        Assert.Equal(250_000m, opening.NetWorth);
    }

    /// <summary>
    /// Every component is reported separately so a UI can stack them, and the
    /// total is nothing but their sum.
    /// </summary>
    [Fact]
    public void Simulate_EveryAssetClass_IsReportedSeparatelyAndSumsToTheTotal()
    {
        var bank = TestPlanBuilder.CreateBankAccount(openingBalance: 10_000m);
        var savings = TestPlanBuilder.CreateSavingsAccount(openingBalance: 25_000m);
        var cash = NewAccount(AccountType.Cash, "Wallet", 500m);
        var investment = NewAccount(AccountType.Investment, "Portfolio", 80_000m);
        var owner = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };

        var pillar3a = TestPlanBuilder.CreatePillar3aAccount(
            openingBalance: 60_000m,
            ownerPersonId: owner.Id);
        var card = TestPlanBuilder.CreateCreditCardAccount(openingBalance: -1_200m);
        var loan = NewAccount(AccountType.Loan, "Car loan", -8_000m);
        var external = NewAccount(AccountType.External, "Employer", 999_999m);

        var plan = TestPlanBuilder.CreatePlan(
            persons: [owner],
            accounts: [bank, savings, cash, investment, pillar3a, card, loan, external],
            startDate: Start,
            endDate: End);

        var point = new SimulationEngine().Simulate(plan).NetWorthPoints[0];

        Assert.Equal(35_500m, point.LiquidAssets);
        Assert.Equal(80_000m, point.InvestmentAssets);
        Assert.Equal(60_000m, point.Pillar3aAssets);
        Assert.Equal(0m, point.RealEstateValue);
        Assert.Equal(0m, point.MortgagePrincipal);

        // Both liability accounts, taken as owed. The external account is not
        // household wealth and contributes nothing.
        Assert.Equal(9_200m, point.OtherLiabilities);

        Assert.Equal(175_500m, point.TotalAssets);
        Assert.Equal(
            point.TotalAssets - point.TotalLiabilities,
            point.NetWorth);
        Assert.Equal(166_300m, point.NetWorth);
    }

    [Fact]
    public void Simulate_ProducesOneNetWorthPointPerSimulatedDay()
    {
        var plan = TestPlanBuilder.CreatePlan(
            accounts: [TestPlanBuilder.CreateBankAccount(openingBalance: 1_000m)],
            startDate: Start,
            endDate: new DateOnly(2026, 1, 31));

        var result = new SimulationEngine().Simulate(plan);

        Assert.Equal(31, result.NetWorthPoints.Count);
        Assert.Equal(Start, result.NetWorthPoints[0].Date);
        Assert.Equal(new DateOnly(2026, 1, 31), result.NetWorthPoints[^1].Date);
        Assert.All(result.NetWorthPoints, p => Assert.Equal("CHF", p.Currency));
    }

    /// <summary>
    /// Direct amortisation is the case where the two series move in opposite
    /// directions on the same day: the bank account drops and the debt drops
    /// with it, so net worth is flat except for the interest.
    /// </summary>
    [Fact]
    public void Simulate_DirectAmortisation_ReducesTheMortgageComponentOverTime()
    {
        var bank = TestPlanBuilder.CreateBankAccount(openingBalance: 100_000m);

        var mortgage = new MortgageContract
        {
            Id = Guid.NewGuid(),
            Name = "Mortgage",
            Type = MortgageType.Fixed,
            PaymentAccountId = bank.Id,
            InitialPrincipal = 400_000m,
            InitialDate = Start,
            CalculationPrincipal = 400_000m,
            CalculationPrincipalDate = Start,
            FixedInterestPercent = 1m,
            AmortisationMode = AmortisationMode.Direct,
            AnnualAmortisationAmount = 8_000m
        };

        var plan = TestPlanBuilder.CreatePlan(
            accounts: [bank],
            mortgages: [mortgage],
            startDate: Start,
            endDate: End);

        var result = new SimulationEngine().Simulate(plan);

        var first = result.NetWorthPoints[0];
        var last = result.NetWorthPoints[^1];

        Assert.Equal(400_000m, first.MortgagePrincipal);
        Assert.True(
            last.MortgagePrincipal < first.MortgagePrincipal,
            "direct amortisation must reduce the reported principal");

        // Only the interest actually leaves the household.
        Assert.True(last.NetWorth < first.NetWorth);
        Assert.True(last.NetWorth > first.NetWorth - 10_000m);
    }

    [Theory]
    // No growth assumption: the property is held flat, which is what the type
    // did before it had a valuation date at all.
    [InlineData(0, 2026, 900_000)]
    [InlineData(0, 2036, 900_000)]
    // 2% a year, compounded on the anniversary of the valuation date.
    [InlineData(2, 2026, 900_000)]
    [InlineData(2, 2027, 918_000)]
    [InlineData(2, 2028, 936_360)]
    public void RealEstateAsset_ValueCompoundsFromTheValuationDate(
        int growthPercent,
        int year,
        int expected)
    {
        var asset = new RealEstateAsset
        {
            Name = "Flat",
            CurrentEstimatedValue = 900_000m,
            ValuationDate = new DateOnly(2026, 1, 1),
            AnnualValueGrowthPercent = growthPercent
        };

        Assert.Equal(expected, asset.GetValueOn(new DateOnly(year, 6, 30)));
    }

    private static Account NewAccount(
        AccountType type,
        string name,
        decimal openingBalance)
    {
        return new Account
        {
            Id = Guid.NewGuid(),
            Name = name,
            Type = type,
            Currency = "CHF",
            OpeningBalance = openingBalance,
            OpeningDate = Start,
            IsActive = true
        };
    }
}
