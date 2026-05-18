using CashFlowPlanner.Core.RealEstate;
using Xunit;

namespace CashFlowPlanner.Core.Tests.RealEstate;

public sealed class SwissMortgageEdgeCaseTests
{
    [Fact]
    public void Calculate_ShouldThrow_WhenBuyPriceIsZero()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 0m,
            DesiredMortgage = 0m
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new SwissMortgageAffordabilityCalculator().Calculate(scenario));

        Assert.Equal("Buy price must be > 0.", ex.Message);
    }

    [Fact]
    public void Should_Handle_No_Equity()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 1_000_000m,
            DesiredMortgage = 800_000m,
            Incomes =
            [
                new PersonIncome { Name="P", GrossAnnualIncome=200_000m }
            ]
        };

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.False(result.Checks.Single(x => x.Code == "EQUITY_TOTAL_MIN").Passed);
    }

    [Fact]
    public void Should_Handle_Zero_Income()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 1_000_000m,
            DesiredMortgage = 500_000m
        };

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.False(result.IsViable);
        Assert.True(result.AffordabilityRatio >= 1m);
    }

    [Fact]
    public void Should_Handle_Exactly_On_Boundary_Conditions()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 1_000_000m,
            DesiredMortgage = 800_000m,
            EquitySources =
            [
                new EquitySource { Name="Cash", Type=EquitySourceType.Cash, Amount=100_000m },
                new EquitySource { Name="BVG", Type=EquitySourceType.Pillar2Bvg, Amount=100_000m }
            ],
            Incomes =
            [
                new PersonIncome { Name="P", GrossAnnualIncome=216_000m } // tuned to hit ~33%
            ]
        };

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.True(result.Checks.Single(x => x.Code == "EQUITY_TOTAL_MIN").Passed);
        Assert.True(result.Checks.Single(x => x.Code == "EQUITY_HARD_MIN").Passed);
        Assert.True(result.Checks.Single(x => x.Code == "EQUITY_PILLAR2_MAX").Passed);
        Assert.True(result.Checks.Single(x => x.Code == "MORTGAGE_LTV_MAX").Passed);
    }

    [Fact]
    public void Should_Handle_No_Incomes_List()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 1_000_000m,
            DesiredMortgage = 500_000m
        };

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.False(result.IsViable);
    }

    [Fact]
    public void Should_Handle_Very_Large_Values()
    {
        var scenario = new HousePurchaseScenario
        {
            BuyPrice = 100_000_000m,
            DesiredMortgage = 80_000_000m,
            EquitySources =
            [
                new EquitySource { Name="Cash", Type=EquitySourceType.Cash, Amount=20_000_000m }
            ],
            Incomes =
            [
                new PersonIncome { Name="P", GrossAnnualIncome=10_000_000m }
            ]
        };

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.NotNull(result);
    }
}
