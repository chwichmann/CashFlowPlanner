using CashFlowPlanner.Core.RealEstate;
using Xunit;

namespace CashFlowPlanner.Core.Tests.RealEstate;

public sealed class HouseSaleCalculatorTests
{
    [Fact]
    public void Calculate_ShouldSplitProceedsIntoFreeCashAndPillar2()
    {
        var calculator = new HouseSaleCalculator();

        var scenario = new HouseSaleScenario
        {
            ExpectedSalePrice = 920_000m,
            RemainingMortgagePrincipal = 720_000m,
            SellingCosts = 0m,
            Pillar2BvgBoundAmount = 74_000m
        };

        var result = calculator.Calculate(scenario);

        Assert.Equal(200_000m, result.NetProceeds);
        Assert.Equal(74_000m, result.Pillar2BoundAmount);
        Assert.Equal(126_000m, result.FreeCashAmount);
    }

    [Fact]
    public void Calculate_ShouldCapPillar2ToNetProceeds()
    {
        var calculator = new HouseSaleCalculator();

        var scenario = new HouseSaleScenario
        {
            ExpectedSalePrice = 800_000m,
            RemainingMortgagePrincipal = 780_000m,
            SellingCosts = 0m,
            Pillar2BvgBoundAmount = 50_000m // higher than net proceeds
        };

        var result = calculator.Calculate(scenario);

        Assert.Equal(20_000m, result.NetProceeds);
        Assert.Equal(20_000m, result.Pillar2BoundAmount);
        Assert.Equal(0m, result.FreeCashAmount);
    }

    [Fact]
    public void Calculate_ShouldReturnZeroFreeCash_WhenNetNegative()
    {
        var calculator = new HouseSaleCalculator();

        var scenario = new HouseSaleScenario
        {
            ExpectedSalePrice = 700_000m,
            RemainingMortgagePrincipal = 720_000m,
            SellingCosts = 0m,
            Pillar2BvgBoundAmount = 50_000m
        };

        var result = calculator.Calculate(scenario);

        Assert.Equal(-20_000m, result.NetProceeds);
        Assert.Equal(0m, result.Pillar2BoundAmount);
        Assert.Equal(0m, result.FreeCashAmount);
    }
}
