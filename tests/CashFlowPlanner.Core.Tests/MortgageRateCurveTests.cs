using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Mortgages;

namespace CashFlowPlanner.Core.Tests;

public sealed class MortgageRateCurveTests
{
    [Fact]
    public void GetRatePercent_Should_ReturnZero_WhenNoPointsExist()
    {
        var curve = new MortgageRateCurve([]);

        var rate = curve.GetRatePercent(new DateOnly(2026, 1, 1));

        Assert.Equal(0m, rate);
    }

    [Fact]
    public void GetRatePercent_Should_ReturnFirstRate_BeforeFirstPoint()
    {
        var curve = new MortgageRateCurve(
        [
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 1),
                RatePercent = 1.2m
            }
        ]);

        var rate = curve.GetRatePercent(new DateOnly(2025, 12, 1));

        Assert.Equal(1.2m, rate);
    }

    [Fact]
    public void GetRatePercent_Should_ReturnLastRate_AfterLastPoint()
    {
        var curve = new MortgageRateCurve(
        [
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 1),
                RatePercent = 1.2m
            }
        ]);

        var rate = curve.GetRatePercent(new DateOnly(2027, 1, 1));

        Assert.Equal(1.2m, rate);
    }

    [Fact]
    public void GetRatePercent_Should_InterpolateLinearly_BetweenTwoPoints()
    {
        var curve = new MortgageRateCurve(
        [
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 1),
                RatePercent = 1.0m
            },
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 11),
                RatePercent = 2.0m
            }
        ]);

        var rate = curve.GetRatePercent(new DateOnly(2026, 1, 6));

        Assert.Equal(1.5m, rate);
    }

    [Fact]
    public void GetRatePercent_Should_SortPointsBeforeInterpolation()
    {
        var curve = new MortgageRateCurve(
        [
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 11),
                RatePercent = 2.0m
            },
            new MortgageInterestRatePoint
            {
                Date = new DateOnly(2026, 1, 1),
                RatePercent = 1.0m
            }
        ]);

        var rate = curve.GetRatePercent(new DateOnly(2026, 1, 6));

        Assert.Equal(1.5m, rate);
    }
}