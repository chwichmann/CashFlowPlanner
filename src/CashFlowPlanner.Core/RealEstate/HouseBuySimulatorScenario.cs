namespace CashFlowPlanner.Core.RealEstate;

public sealed class HouseBuySimulatorScenario
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "House buy scenario";

    public DateTimeOffset CreatedUtc { get; init; } = DateTimeOffset.UtcNow;

    public DateTimeOffset ModifiedUtc { get; init; } = DateTimeOffset.UtcNow;

    public decimal SalePrice { get; init; } = 920_000m;

    public decimal SaleRemainingMortgage { get; init; } = 720_000m;

    public decimal SaleCosts { get; init; }

    public decimal SalePillar2BoundAmount { get; init; } = 74_000m;

    public decimal BuyPrice { get; init; } = 1_000_000m;

    public decimal RenovationPrice { get; init; }

    public decimal DesiredMortgage { get; init; } = 800_000m;

    public List<HouseBuyScenarioPerson> Persons { get; init; } = [];

    public List<HouseBuyScenarioEquitySource> EquitySources { get; init; } = [];

    public SwissMortgageRuleSettings Rules { get; init; } = new();

    public decimal TotalPurchasePrice => BuyPrice + RenovationPrice;

    public decimal TotalGrossAnnualIncome =>
        Persons.Sum(x => x.GrossAnnualIncome);

    public decimal CashEquity =>
        EquitySources
            .Where(x => x.Type == EquitySourceType.Cash)
            .Sum(x => x.Amount);

    public decimal Pillar2BvgEquity =>
        EquitySources
            .Where(x => x.Type == EquitySourceType.Pillar2Bvg)
            .Sum(x => x.Amount);

    public decimal TotalEquity => CashEquity + Pillar2BvgEquity;

    public static HouseBuySimulatorScenario CreateDefault(
        string name = "House buy scenario")
    {
        return new HouseBuySimulatorScenario
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name)
                ? "House buy scenario"
                : name.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,
            SalePrice = 920_000m,
            SaleRemainingMortgage = 720_000m,
            SaleCosts = 0m,
            SalePillar2BoundAmount = 74_000m,
            BuyPrice = 1_000_000m,
            RenovationPrice = 0m,
            DesiredMortgage = 800_000m,
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "Person 1",
                    GrossAnnualIncome = 200_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash equity",
                    Type = EquitySourceType.Cash,
                    Amount = 150_000m,
                    Origin = EquitySourceOrigin.Manual
                },
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Pillar 2 BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 50_000m,
                    Origin = EquitySourceOrigin.Manual
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };
    }

    public HouseBuySimulatorScenario WithModifiedTimestamp()
    {
        return new HouseBuySimulatorScenario
        {
            Id = Id,
            Name = Name,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = DateTimeOffset.UtcNow,

            SalePrice = SalePrice,
            SaleRemainingMortgage = SaleRemainingMortgage,
            SaleCosts = SaleCosts,
            SalePillar2BoundAmount = SalePillar2BoundAmount,

            BuyPrice = BuyPrice,
            RenovationPrice = RenovationPrice,
            DesiredMortgage = DesiredMortgage,

            Persons = Persons,
            EquitySources = EquitySources,
            Rules = Rules
        };
    }

    public HouseBuySimulatorScenario CopyAsNew(
        string name)
    {
        var personIdMap = Persons.ToDictionary(
            x => x.Id,
            _ => Guid.NewGuid());

        return new HouseBuySimulatorScenario
        {
            Id = Guid.NewGuid(),
            Name = string.IsNullOrWhiteSpace(name)
                ? $"{Name} copy"
                : name.Trim(),
            CreatedUtc = DateTimeOffset.UtcNow,
            ModifiedUtc = DateTimeOffset.UtcNow,

            SalePrice = SalePrice,
            SaleRemainingMortgage = SaleRemainingMortgage,
            SaleCosts = SaleCosts,
            SalePillar2BoundAmount = SalePillar2BoundAmount,

            BuyPrice = BuyPrice,
            RenovationPrice = RenovationPrice,
            DesiredMortgage = DesiredMortgage,

            Persons = Persons
                .Select(x => new HouseBuyScenarioPerson
                {
                    Id = personIdMap[x.Id],
                    Name = x.Name,
                    GrossAnnualIncome = x.GrossAnnualIncome
                })
                .ToList(),

            EquitySources = EquitySources
                .Select(x => new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = x.Name,
                    PersonId = x.PersonId is not null &&
                               personIdMap.TryGetValue(x.PersonId.Value, out var mappedPersonId)
                        ? mappedPersonId
                        : null,
                    Type = x.Type,
                    Amount = x.Amount,
                    Origin = x.Origin
                })
                .ToList(),

            Rules = new SwissMortgageRuleSettings
            {
                MaxLoanToValuePercent = Rules.MaxLoanToValuePercent,
                MinTotalEquityPercent = Rules.MinTotalEquityPercent,
                MinHardEquityPercent = Rules.MinHardEquityPercent,
                MaxPillar2Percent = Rules.MaxPillar2Percent,
                FirstMortgageThresholdPercent = Rules.FirstMortgageThresholdPercent,
                AmortisationYears = Rules.AmortisationYears,
                ImputedInterestPercent = Rules.ImputedInterestPercent,
                MaintenancePercent = Rules.MaintenancePercent,
                MaxAffordabilityPercent = Rules.MaxAffordabilityPercent
            }
        };
    }

    public void Validate()
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException("House buy scenario id is required.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException("House buy scenario name is required.");
        }

        if (SalePrice < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' sale price must not be negative.");
        }

        if (SaleRemainingMortgage < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' remaining mortgage must not be negative.");
        }

        if (SaleCosts < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' selling costs must not be negative.");
        }

        if (SalePillar2BoundAmount < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' Pillar 2 BVG amount must not be negative.");
        }

        if (BuyPrice <= 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' buy price must be greater than zero.");
        }

        if (RenovationPrice < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' renovation price must not be negative.");
        }

        if (DesiredMortgage < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' desired mortgage must not be negative.");
        }

        if (Rules is null)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' requires rule settings.");
        }

        ValidatePersons();
        ValidateEquitySources();
    }

    private void ValidatePersons()
    {
        foreach (var person in Persons)
        {
            person.Validate(Name);
        }

        var duplicatePersonIds = Persons
            .GroupBy(x => x.Id)
            .Where(x => x.Key != Guid.Empty && x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicatePersonIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' contains duplicate person ids.");
        }
    }

    private void ValidateEquitySources()
    {
        foreach (var equitySource in EquitySources)
        {
            equitySource.Validate(Name);
        }

        var personIds = Persons
            .Select(x => x.Id)
            .ToHashSet();

        var invalidPersonLink = EquitySources
            .Where(x => x.PersonId is not null)
            .FirstOrDefault(x => !personIds.Contains(x.PersonId!.Value));

        if (invalidPersonLink is not null)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' equity source '{invalidPersonLink.Name}' references an unknown person.");
        }

        var duplicateEquityIds = EquitySources
            .GroupBy(x => x.Id)
            .Where(x => x.Key != Guid.Empty && x.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        if (duplicateEquityIds.Count > 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{Name}' contains duplicate equity source ids.");
        }
    }

    private HouseBuySimulatorScenario withModifiedUtc(
        DateTimeOffset modifiedUtc)
    {
        return new HouseBuySimulatorScenario
        {
            Id = Id,
            Name = Name,
            CreatedUtc = CreatedUtc,
            ModifiedUtc = modifiedUtc,

            SalePrice = SalePrice,
            SaleRemainingMortgage = SaleRemainingMortgage,
            SaleCosts = SaleCosts,
            SalePillar2BoundAmount = SalePillar2BoundAmount,

            BuyPrice = BuyPrice,
            RenovationPrice = RenovationPrice,
            DesiredMortgage = DesiredMortgage,

            Persons = Persons,
            EquitySources = EquitySources,
            Rules = Rules
        };
    }
}

public sealed class HouseBuyScenarioPerson
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "";

    public decimal GrossAnnualIncome { get; init; }

    public void Validate(string scenarioName)
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' contains a person without id.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' contains a person without name.");
        }

        if (GrossAnnualIncome < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' person '{Name}' gross annual income must not be negative.");
        }
    }
}

public sealed class HouseBuyScenarioEquitySource
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public string Name { get; init; } = "";

    public Guid? PersonId { get; init; }

    public EquitySourceType Type { get; init; }

    public decimal Amount { get; init; }

    public EquitySourceOrigin Origin { get; init; } = EquitySourceOrigin.Manual;

    public void Validate(string scenarioName)
    {
        if (Id == Guid.Empty)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' contains an equity source without id.");
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' contains an equity source without name.");
        }

        if (Amount < 0)
        {
            throw new InvalidOperationException(
                $"House buy scenario '{scenarioName}' equity source '{Name}' amount must not be negative.");
        }
    }
}