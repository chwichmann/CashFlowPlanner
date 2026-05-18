namespace CashFlowPlanner.Core.RealEstate;

public sealed class SwissMortgageAffordabilityCalculator
{
    public HousePurchaseResult Calculate(HousePurchaseScenario scenario)
    {
        ArgumentNullException.ThrowIfNull(scenario);

        scenario.Validate();

        var rules = scenario.Rules;

        ValidateRules(rules);

        var totalPrice = scenario.BuyPrice + scenario.RenovationPrice;

        var cashEquity = scenario.EquitySources
            .Where(x => x.Type == EquitySourceType.Cash)
            .Sum(x => x.Amount);

        var pillar2Equity = scenario.EquitySources
            .Where(x => x.Type == EquitySourceType.Pillar2Bvg)
            .Sum(x => x.Amount);

        var totalEquity = cashEquity + pillar2Equity;

        var mortgage = scenario.DesiredMortgage;

        var grossAnnualIncome = scenario.Incomes
            .Sum(x => x.GrossAnnualIncome);

        var loanToValuePercent = totalPrice <= 0
            ? 0m
            : mortgage / totalPrice * 100m;

        // Rule limits
        var requiredTotalEquity = PercentOf(totalPrice, rules.MinTotalEquityPercent);
        var requiredHardEquity = PercentOf(totalPrice, rules.MinHardEquityPercent);
        var maxPillar2Equity = PercentOf(totalPrice, rules.MaxPillar2Percent);
        var maxMortgage = PercentOf(totalPrice, rules.MaxLoanToValuePercent);

        // Affordability calculation
        var theoreticalYearlyInterest = PercentOf(mortgage, rules.ImputedInterestPercent);
        var theoreticalYearlyMaintenance = PercentOf(totalPrice, rules.MaintenancePercent);

        var firstMortgageLimit = PercentOf(totalPrice, rules.FirstMortgageThresholdPercent);
        var secondMortgageAmount = Math.Max(0m, mortgage - firstMortgageLimit);
        var requiredYearlyAmortisation = secondMortgageAmount / rules.AmortisationYears;

        var theoreticalYearlyCost =
            theoreticalYearlyInterest
            + theoreticalYearlyMaintenance
            + requiredYearlyAmortisation;

        var affordabilityLimitRatio = rules.MaxAffordabilityPercent / 100m;

        var maxAllowedYearlyCost =
            grossAnnualIncome * affordabilityLimitRatio;

        var affordabilityRatio = grossAnnualIncome <= 0
            ? decimal.MaxValue
            : theoreticalYearlyCost / grossAnnualIncome;

        var requiredGrossAnnualIncome = affordabilityLimitRatio <= 0
            ? decimal.MaxValue
            : theoreticalYearlyCost / affordabilityLimitRatio;

        var missingGrossAnnualIncome = grossAnnualIncome <= 0
            ? requiredGrossAnnualIncome
            : Math.Max(0m, requiredGrossAnnualIncome - grossAnnualIncome);

        var checks = new List<RuleCheckResult>
        {
            new()
            {
                Code = "EQUITY_TOTAL_MIN",
                Description = $"Total equity must be at least {rules.MinTotalEquityPercent:N0}% of the total price.",
                Passed = totalEquity >= requiredTotalEquity,
                ActualValue = totalEquity,
                RequiredValue = requiredTotalEquity
            },
            new()
            {
                Code = "EQUITY_HARD_MIN",
                Description = $"Cash / hard equity must be at least {rules.MinHardEquityPercent:N0}% of the total price.",
                Passed = cashEquity >= requiredHardEquity,
                ActualValue = cashEquity,
                RequiredValue = requiredHardEquity
            },
            new()
            {
                Code = "EQUITY_PILLAR2_MAX",
                Description = $"Pillar 2 BVG equity must not exceed {rules.MaxPillar2Percent:N0}% of the total price.",
                Passed = pillar2Equity <= maxPillar2Equity,
                ActualValue = pillar2Equity,
                RequiredValue = maxPillar2Equity
            },
            new()
            {
                Code = "MORTGAGE_LTV_MAX",
                Description = $"Mortgage must not exceed {rules.MaxLoanToValuePercent:N0}% of the total price.",
                Passed = mortgage <= maxMortgage,
                ActualValue = mortgage,
                RequiredValue = maxMortgage
            },
            new()
            {
                Code = "AFFORDABILITY_MAX",
                Description = $"Theoretical yearly costs must not exceed {rules.MaxAffordabilityPercent:N0}% of gross income.",
                Passed = theoreticalYearlyCost <= maxAllowedYearlyCost,
                ActualValue = theoreticalYearlyCost,
                RequiredValue = maxAllowedYearlyCost
            }
        };

        return new HousePurchaseResult
        {
            TotalPrice = totalPrice,

            CashEquity = cashEquity,
            Pillar2Equity = pillar2Equity,
            TotalEquity = totalEquity,

            LoanToValuePercent = loanToValuePercent,

            TheoreticalYearlyCost = theoreticalYearlyCost,
            AffordabilityRatio = affordabilityRatio,

            IsAffordable = theoreticalYearlyCost <= maxAllowedYearlyCost,

            GrossAnnualIncome = grossAnnualIncome,
            MaxAllowedYearlyCost = maxAllowedYearlyCost,
            RequiredGrossAnnualIncomeForAffordability = requiredGrossAnnualIncome,
            MissingGrossAnnualIncomeForAffordability = missingGrossAnnualIncome,

            Checks = checks
        };
    }

    private static decimal PercentOf(decimal amount, decimal percent)
    {
        return amount * percent / 100m;
    }

    private static void ValidateRules(SwissMortgageRuleSettings rules)
    {
        if (rules.MaxLoanToValuePercent <= 0)
        {
            throw new InvalidOperationException("Maximum loan-to-value percent must be greater than zero.");
        }

        if (rules.MinTotalEquityPercent < 0)
        {
            throw new InvalidOperationException("Minimum total equity percent must not be negative.");
        }

        if (rules.MinHardEquityPercent < 0)
        {
            throw new InvalidOperationException("Minimum hard equity percent must not be negative.");
        }

        if (rules.MaxPillar2Percent < 0)
        {
            throw new InvalidOperationException("Maximum Pillar 2 percent must not be negative.");
        }

        if (rules.FirstMortgageThresholdPercent < 0)
        {
            throw new InvalidOperationException("First mortgage threshold percent must not be negative.");
        }

        if (rules.AmortisationYears <= 0)
        {
            throw new InvalidOperationException("Amortisation years must be greater than zero.");
        }

        if (rules.ImputedInterestPercent < 0)
        {
            throw new InvalidOperationException("Imputed interest percent must not be negative.");
        }

        if (rules.MaintenancePercent < 0)
        {
            throw new InvalidOperationException("Maintenance percent must not be negative.");
        }

        if (rules.MaxAffordabilityPercent <= 0)
        {
            throw new InvalidOperationException("Maximum affordability percent must be greater than zero.");
        }
    }
}