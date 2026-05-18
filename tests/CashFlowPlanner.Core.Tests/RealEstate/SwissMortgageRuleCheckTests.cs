using CashFlowPlanner.Core.RealEstate;
using Xunit;

namespace CashFlowPlanner.Core.Tests.RealEstate;

public sealed class SwissMortgageRuleCheckTests
{
    [Fact]
    public void Calculate_ShouldPassAllRules_ForValidScenario()
    {
        var scenario = CreateScenario();

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.All(result.Checks, check => Assert.True(check.Passed));
        Assert.True(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldFailTotalEquity_WhenBelow20Percent()
    {
        var scenario = CreateScenario(
            mortgage: 850_000m,
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 150_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_TOTAL_MIN");

        Assert.False(check.Passed);
        Assert.Equal(150_000m, check.ActualValue);
        Assert.Equal(200_000m, check.RequiredValue);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldPassTotalEquity_WhenExactly20Percent()
    {
        var scenario = CreateScenario(
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 100_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_TOTAL_MIN");

        Assert.True(check.Passed);
        Assert.Equal(200_000m, check.ActualValue);
        Assert.Equal(200_000m, check.RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldFailHardEquity_WhenCashBelow10Percent()
    {
        var scenario = CreateScenario(
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 99_999m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_001m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_HARD_MIN");

        Assert.False(check.Passed);
        Assert.Equal(99_999m, check.ActualValue);
        Assert.Equal(100_000m, check.RequiredValue);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldPassHardEquity_WhenExactly10Percent()
    {
        var scenario = CreateScenario(
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 100_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_HARD_MIN");

        Assert.True(check.Passed);
        Assert.Equal(100_000m, check.ActualValue);
        Assert.Equal(100_000m, check.RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldFailPillar2_WhenAbove10Percent()
    {
        var scenario = CreateScenario(
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 100_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_001m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_PILLAR2_MAX");

        Assert.False(check.Passed);
        Assert.Equal(100_001m, check.ActualValue);
        Assert.Equal(100_000m, check.RequiredValue);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldPassPillar2_WhenExactly10Percent()
    {
        var scenario = CreateScenario(
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 100_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "EQUITY_PILLAR2_MAX");

        Assert.True(check.Passed);
        Assert.Equal(100_000m, check.ActualValue);
        Assert.Equal(100_000m, check.RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldFailMortgageLtv_WhenMortgageAbove80Percent()
    {
        var scenario = CreateScenario(
            mortgage: 800_001m,
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 199_999m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "MORTGAGE_LTV_MAX");

        Assert.False(check.Passed);
        Assert.Equal(800_001m, check.ActualValue);
        Assert.Equal(800_000m, check.RequiredValue);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldPassMortgageLtv_WhenExactly80Percent()
    {
        var scenario = CreateScenario(
            mortgage: 800_000m,
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 200_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "MORTGAGE_LTV_MAX");

        Assert.True(check.Passed);
        Assert.Equal(800_000m, check.ActualValue);
        Assert.Equal(800_000m, check.RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldFailAffordability_WhenTheoreticalCostAbove33PercentOfIncome()
    {
        var scenario = CreateScenario(
            income: 100_000m);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "AFFORDABILITY_MAX");

        Assert.False(check.Passed);
        Assert.True(check.ActualValue > check.RequiredValue);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldPassAffordability_WhenTheoreticalCostBelow33PercentOfIncome()
    {
        var scenario = CreateScenario(
            income: 200_000m);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        var check = GetCheck(result, "AFFORDABILITY_MAX");

        Assert.True(check.Passed);
        Assert.True(check.ActualValue <= check.RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldUseBuyPricePlusRenovationPrice_ForAllRuleLimits()
    {
        var scenario = CreateScenario(
            buyPrice: 900_000m,
            renovationPrice: 100_000m,
            mortgage: 800_000m,
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 100_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 100_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.Equal(1_000_000m, result.TotalPrice);

        Assert.Equal(200_000m, GetCheck(result, "EQUITY_TOTAL_MIN").RequiredValue);
        Assert.Equal(100_000m, GetCheck(result, "EQUITY_HARD_MIN").RequiredValue);
        Assert.Equal(100_000m, GetCheck(result, "EQUITY_PILLAR2_MAX").RequiredValue);
        Assert.Equal(800_000m, GetCheck(result, "MORTGAGE_LTV_MAX").RequiredValue);
    }

    [Fact]
    public void Calculate_ShouldFailMultipleRules_WhenScenarioIsClearlyInvalid()
    {
        var scenario = CreateScenario(
            mortgage: 900_000m,
            income: 80_000m,
            equitySources:
            [
                new EquitySource
                {
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 50_000m
                },
                new EquitySource
                {
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 150_000m
                }
            ]);

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.False(GetCheck(result, "EQUITY_HARD_MIN").Passed);
        Assert.False(GetCheck(result, "EQUITY_PILLAR2_MAX").Passed);
        Assert.False(GetCheck(result, "MORTGAGE_LTV_MAX").Passed);
        Assert.False(GetCheck(result, "AFFORDABILITY_MAX").Passed);
        Assert.False(result.IsViable);
    }

    [Fact]
    public void Calculate_ShouldCreateExactlyFiveRuleChecks()
    {
        var scenario = CreateScenario();

        var result = new SwissMortgageAffordabilityCalculator().Calculate(scenario);

        Assert.Equal(5, result.Checks.Count);

        Assert.Contains(result.Checks, x => x.Code == "EQUITY_TOTAL_MIN");
        Assert.Contains(result.Checks, x => x.Code == "EQUITY_HARD_MIN");
        Assert.Contains(result.Checks, x => x.Code == "EQUITY_PILLAR2_MAX");
        Assert.Contains(result.Checks, x => x.Code == "MORTGAGE_LTV_MAX");
        Assert.Contains(result.Checks, x => x.Code == "AFFORDABILITY_MAX");
    }

    private static RuleCheckResult GetCheck(
        HousePurchaseResult result,
        string code)
    {
        return result.Checks.Single(x => x.Code == code);
    }

    private static HousePurchaseScenario CreateScenario(
        decimal buyPrice = 1_000_000m,
        decimal renovationPrice = 0m,
        decimal mortgage = 800_000m,
        decimal income = 200_000m,
        List<EquitySource>? equitySources = null,
        List<PersonIncome>? incomes = null,
        SwissMortgageRuleSettings? rules = null)
    {
        return new HousePurchaseScenario
        {
            BuyPrice = buyPrice,
            RenovationPrice = renovationPrice,
            DesiredMortgage = mortgage,
            EquitySources = equitySources ?? DefaultEquitySources(),
            Incomes = incomes ?? DefaultIncomes(income),
            Rules = rules ?? new SwissMortgageRuleSettings()
        };
    }

    private static List<EquitySource> DefaultEquitySources()
    {
        return
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
        ];
    }

    private static List<PersonIncome> DefaultIncomes(decimal income)
    {
        return
        [
            new PersonIncome
            {
                Name = "Person",
                GrossAnnualIncome = income
            }
        ];
    }
}