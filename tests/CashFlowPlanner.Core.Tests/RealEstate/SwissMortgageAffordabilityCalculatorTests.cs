using CashFlowPlanner.Core.RealEstate;
using Xunit;

namespace CashFlowPlanner.Core.Tests.RealEstate;

public sealed class SwissMortgageAffordabilityCalculatorTests
{
    private static HousePurchaseScenario CreateBaseScenario()
    {
        return new HousePurchaseScenario
        {
            BuyPrice = 1_000_000m,
            RenovationPrice = 0m,
            DesiredMortgage = 800_000m,
            EquitySources =
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 150_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 50_000m
                }
            ],
            Incomes =
            [
                new PersonIncome
                {
                    Name = "Person",
                    GrossAnnualIncome = 200_000m
                }
            ]
        };
    }

    [Fact]
    public void Calculate_ShouldComputeTotalPriceCorrectly()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        Assert.Equal(1_000_000m, result.TotalPrice);
    }

    [Fact]
    public void Calculate_ShouldComputeEquityBreakdown()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        Assert.Equal(150_000m, result.CashEquity);
        Assert.Equal(50_000m, result.Pillar2Equity);
        Assert.Equal(200_000m, result.TotalEquity);
    }

    [Fact]
    public void Calculate_ShouldComputeLoanToValue()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        Assert.Equal(80m, result.LoanToValuePercent);
    }

    [Fact]
    public void Calculate_ShouldComputeAmortisationAbove66Percent()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        // manual verification:
        // 66% of 1,000,000 = 660,000
        // mortgage = 800,000
        // second mortgage = 140,000
        // amortisation = 140,000 / 15 = 9,333.33

        var expectedAmortisation = 140_000m / 15m;

        var rules = scenario.Rules;

        var firstLimit = 1_000_000m * (rules.FirstMortgageThresholdPercent / 100m);
        var secondMortgage = scenario.DesiredMortgage - firstLimit;
        var amortisation = secondMortgage / rules.AmortisationYears;

        Assert.Equal(expectedAmortisation, amortisation);
    }

    [Fact]
    public void Calculate_ShouldComputeTheoreticalYearlyCost()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        // interest: 800,000 * 5% = 40,000
        // maintenance: 1,000,000 * 1% = 10,000
        // amortisation: 140,000 / 15 = 9,333.33

        var expected = 40_000m + 10_000m + (140_000m / 15m);

        Assert.Equal(expected, result.TheoreticalYearlyCost);
    }

    [Fact]
    public void Calculate_ShouldBeAffordable_WhenBelow33Percent()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        var result = calculator.Calculate(scenario);

        Assert.True(result.IsAffordable);
        Assert.True(result.AffordabilityRatio < 0.33m);
    }

    [Fact]
    public void Calculate_ShouldNotBeAffordable_WhenAbove33Percent()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        // Reduce income → force failure
        scenario.Incomes[0] = new PersonIncome
        {
            Name = "Person",
            GrossAnnualIncome = 100_000m
        };

        var result = calculator.Calculate(scenario);

        Assert.False(result.IsAffordable);
        Assert.True(result.AffordabilityRatio > 0.33m);
    }

    [Fact]
    public void Calculate_ShouldHandleZeroIncome_AsNotAffordable()
    {
        var calculator = new SwissMortgageAffordabilityCalculator();

        var scenario = CreateBaseScenario();

        scenario.Incomes.Clear();

        var result = calculator.Calculate(scenario);

        Assert.False(result.IsAffordable);
        Assert.False(result.IsViable);

        Assert.Equal(decimal.MaxValue, result.AffordabilityRatio);

        Assert.True(result.RequiredGrossAnnualIncomeForAffordability > 0m);
        Assert.Equal(
            result.RequiredGrossAnnualIncomeForAffordability,
            result.MissingGrossAnnualIncomeForAffordability);
    }
}