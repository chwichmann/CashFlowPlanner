namespace CashFlowPlanner.Core.RealEstate;

public sealed class HouseSaleCalculator
{
    public HouseSaleResult Calculate(HouseSaleScenario scenario)
    {
        scenario.Validate();

        var net = scenario.ExpectedSalePrice
                  - scenario.SellingCosts
                  - scenario.RemainingMortgagePrincipal;

        var positiveNet = Math.Max(0, net);

        var pillar2 = Math.Min(
            scenario.Pillar2BvgBoundAmount,
            positiveNet);

        var freeCash = Math.Max(0, positiveNet - pillar2);

        return new HouseSaleResult
        {
            NetProceeds = net,
            Pillar2BoundAmount = pillar2,
            FreeCashAmount = freeCash
        };
    }
}