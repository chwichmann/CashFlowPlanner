using CashFlowPlanner.Core.RealEstate;
using Xunit;

namespace CashFlowPlanner.Core.Tests.RealEstate;

public sealed class HouseBuyScenarioManagerTests
{
    private readonly HouseBuyScenarioManager _manager = new();

    [Fact]
    public void CreateDefault_Should_ReturnValidScenario()
    {
        var result = _manager.CreateDefault(Array.Empty<HouseBuySimulatorScenario>());

        Assert.NotNull(result);
        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal("House buy scenario", result.Name);
        Assert.Single(result.Persons);
        Assert.Equal(2, result.EquitySources.Count);
    }

    [Fact]
    public void CreateDefault_Should_GenerateUniqueName()
    {
        HouseBuySimulatorScenario[] existing =
        [
            HouseBuySimulatorScenario.CreateDefault("House buy scenario")
        ];

        var result = _manager.CreateDefault(existing);

        Assert.Equal("House buy scenario 2", result.Name);
    }

    [Fact]
    public void AddOrUpdate_Should_AddNewScenario()
    {
        var scenario = HouseBuySimulatorScenario.CreateDefault("A");

        var result = _manager.AddOrUpdate(
            Array.Empty<HouseBuySimulatorScenario>(),
            scenario);

        var stored = Assert.Single(result);

        Assert.Equal("A", stored.Name);
    }

    [Fact]
    public void AddOrUpdate_Should_UpdateExistingScenario()
    {
        var original = HouseBuySimulatorScenario.CreateDefault("A");

        var updated = new HouseBuySimulatorScenario
        {
            Id = original.Id,
            Name = "Updated",
            CreatedUtc = original.CreatedUtc,
            ModifiedUtc = original.ModifiedUtc,

            SalePrice = original.SalePrice,
            SaleRemainingMortgage = original.SaleRemainingMortgage,
            SaleCosts = original.SaleCosts,
            SalePillar2BoundAmount = original.SalePillar2BoundAmount,

            BuyPrice = 999_999m,
            RenovationPrice = original.RenovationPrice,
            DesiredMortgage = original.DesiredMortgage,

            Persons = original.Persons,
            EquitySources = original.EquitySources,
            Rules = original.Rules
        };

        HouseBuySimulatorScenario[] existing =
        [
            original
        ];

        var result = _manager.AddOrUpdate(existing, updated);

        var stored = Assert.Single(result);

        Assert.Equal("Updated", stored.Name);
        Assert.Equal(999_999m, stored.BuyPrice);
        Assert.Equal(original.Id, stored.Id);
    }

    [Fact]
    public void AddOrUpdate_Should_EnsureUniqueName_WhenAddingNewScenario()
    {
        var existingScenario = HouseBuySimulatorScenario.CreateDefault("A");
        var newScenario = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            existingScenario
        ];

        var result = _manager.AddOrUpdate(existing, newScenario);

        Assert.Equal(2, result.Count);
        Assert.Contains(result, x => x.Name == "A");
        Assert.Contains(result, x => x.Name == "A 2");
    }

    [Fact]
    public void AddOrUpdate_Should_NotRenameScenario_WhenUpdatingSameScenarioWithSameName()
    {
        var original = HouseBuySimulatorScenario.CreateDefault("A");

        var updated = new HouseBuySimulatorScenario
        {
            Id = original.Id,
            Name = "A",
            CreatedUtc = original.CreatedUtc,
            ModifiedUtc = original.ModifiedUtc,

            SalePrice = original.SalePrice,
            SaleRemainingMortgage = original.SaleRemainingMortgage,
            SaleCosts = original.SaleCosts,
            SalePillar2BoundAmount = original.SalePillar2BoundAmount,

            BuyPrice = 1_250_000m,
            RenovationPrice = original.RenovationPrice,
            DesiredMortgage = original.DesiredMortgage,

            Persons = original.Persons,
            EquitySources = original.EquitySources,
            Rules = original.Rules
        };

        HouseBuySimulatorScenario[] existing =
        [
            original
        ];

        var result = _manager.AddOrUpdate(existing, updated);

        var stored = Assert.Single(result);

        Assert.Equal("A", stored.Name);
        Assert.Equal(1_250_000m, stored.BuyPrice);
    }

    [Fact]
    public void Rename_Should_UpdateName()
    {
        var scenario = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            scenario
        ];

        var result = _manager.Rename(existing, scenario.Id, "B");

        Assert.Equal("B", result.Single().Name);
    }

    [Fact]
    public void Rename_Should_EnsureUniqueName()
    {
        var a = HouseBuySimulatorScenario.CreateDefault("A");
        var b = HouseBuySimulatorScenario.CreateDefault("B");

        HouseBuySimulatorScenario[] existing =
        [
            a,
            b
        ];

        var result = _manager.Rename(existing, b.Id, "A");

        Assert.Contains(result, x => x.Id == a.Id && x.Name == "A");
        Assert.Contains(result, x => x.Id == b.Id && x.Name == "A 2");
    }

    [Fact]
    public void Rename_Should_Throw_WhenScenarioNotFound()
    {
        HouseBuySimulatorScenario[] existing =
        [
            HouseBuySimulatorScenario.CreateDefault("A")
        ];

        Assert.Throws<InvalidOperationException>(() =>
            _manager.Rename(existing, Guid.NewGuid(), "B"));
    }

    [Fact]
    public void Rename_Should_Throw_WhenNameIsEmpty()
    {
        var scenario = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            scenario
        ];

        Assert.Throws<InvalidOperationException>(() =>
            _manager.Rename(existing, scenario.Id, ""));
    }

    [Fact]
    public void Duplicate_Should_CreateCopy()
    {
        var original = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            original
        ];

        var result = _manager.Duplicate(existing, original.Id);

        Assert.Equal(2, result.Scenarios.Count);
        Assert.NotEqual(original.Id, result.Scenario.Id);
        Assert.Equal("A copy", result.Scenario.Name);
    }

    [Fact]
    public void Duplicate_Should_CreateUniqueCopyName()
    {
        var original = HouseBuySimulatorScenario.CreateDefault("A");
        var existingCopy = original.CopyAsNew("A copy");

        HouseBuySimulatorScenario[] existing =
        [
            original,
            existingCopy
        ];

        var result = _manager.Duplicate(existing, original.Id);

        Assert.Equal("A copy 2", result.Scenario.Name);
        Assert.Equal(3, result.Scenarios.Count);
    }

    [Fact]
    public void Duplicate_Should_CopyPersonsWithNewIds()
    {
        var original = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            original
        ];

        var copy = _manager.Duplicate(existing, original.Id).Scenario;

        Assert.Equal(original.Persons.Count, copy.Persons.Count);

        Assert.DoesNotContain(copy.Persons, copiedPerson =>
            original.Persons.Any(originalPerson => originalPerson.Id == copiedPerson.Id));
    }

    [Fact]
    public void Duplicate_Should_MapEquitySourcesToNewPersons()
    {
        var personId = Guid.NewGuid();

        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Test",
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = personId,
                    Name = "P",
                    GrossAnnualIncome = 100_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    PersonId = personId,
                    Type = EquitySourceType.Cash,
                    Amount = 10_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        HouseBuySimulatorScenario[] existing =
        [
            scenario
        ];

        var copy = _manager.Duplicate(existing, scenario.Id).Scenario;

        Assert.Single(copy.Persons);
        Assert.Single(copy.EquitySources);

        Assert.NotEqual(
            scenario.Persons.First().Id,
            copy.Persons.First().Id);

        Assert.Equal(
            copy.Persons.First().Id,
            copy.EquitySources.First().PersonId);
    }

    [Fact]
    public void Duplicate_Should_Throw_WhenSourceNotFound()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _manager.Duplicate(
                Array.Empty<HouseBuySimulatorScenario>(),
                Guid.NewGuid()));
    }

    [Fact]
    public void Delete_Should_RemoveScenario()
    {
        var a = HouseBuySimulatorScenario.CreateDefault("A");
        var b = HouseBuySimulatorScenario.CreateDefault("B");

        HouseBuySimulatorScenario[] existing =
        [
            a,
            b
        ];

        var result = _manager.Delete(existing, a.Id);

        Assert.Single(result);
        Assert.Equal("B", result.First().Name);
    }

    [Fact]
    public void Delete_Should_ReturnSameList_WhenScenarioDoesNotExist()
    {
        var a = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            a
        ];

        var result = _manager.Delete(existing, Guid.NewGuid());

        Assert.Single(result);
        Assert.Equal(a.Id, result.Single().Id);
    }

    [Fact]
    public void GetRequired_Should_ReturnScenario()
    {
        var scenario = HouseBuySimulatorScenario.CreateDefault("A");

        HouseBuySimulatorScenario[] existing =
        [
            scenario
        ];

        var result = _manager.GetRequired(existing, scenario.Id);

        Assert.Equal(scenario.Id, result.Id);
    }

    [Fact]
    public void GetRequired_Should_ThrowIfNotFound()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _manager.GetRequired(
                Array.Empty<HouseBuySimulatorScenario>(),
                Guid.NewGuid()));
    }

    [Fact]
    public void ScenarioValidate_Should_Throw_WhenNameIsEmpty()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "",
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P",
                    GrossAnnualIncome = 100_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 10_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Validate());
    }

    [Fact]
    public void ScenarioValidate_Should_Throw_WhenBuyPriceIsZero()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Invalid",
            BuyPrice = 0m,
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P",
                    GrossAnnualIncome = 100_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 10_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Validate());
    }

    [Fact]
    public void ScenarioValidate_Should_Throw_WhenPersonIncomeIsNegative()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Invalid",
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P",
                    GrossAnnualIncome = -1m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 10_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Validate());
    }

    [Fact]
    public void ScenarioValidate_Should_Throw_WhenEquityAmountIsNegative()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Invalid",
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P",
                    GrossAnnualIncome = 100_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = -1m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Validate());
    }

    [Fact]
    public void ScenarioValidate_Should_Throw_WhenEquityReferencesUnknownPerson()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Invalid",
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P",
                    GrossAnnualIncome = 100_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    PersonId = Guid.NewGuid(),
                    Type = EquitySourceType.Cash,
                    Amount = 10_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Throws<InvalidOperationException>(() =>
            scenario.Validate());
    }

    [Fact]
    public void ScenarioProperties_Should_CalculateTotals()
    {
        var scenario = new HouseBuySimulatorScenario
        {
            Name = "Totals",
            BuyPrice = 1_000_000m,
            RenovationPrice = 100_000m,
            Persons =
            [
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P1",
                    GrossAnnualIncome = 120_000m
                },
                new HouseBuyScenarioPerson
                {
                    Id = Guid.NewGuid(),
                    Name = "P2",
                    GrossAnnualIncome = 80_000m
                }
            ],
            EquitySources =
            [
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "Cash",
                    Type = EquitySourceType.Cash,
                    Amount = 150_000m
                },
                new HouseBuyScenarioEquitySource
                {
                    Id = Guid.NewGuid(),
                    Name = "BVG",
                    Type = EquitySourceType.Pillar2Bvg,
                    Amount = 50_000m
                }
            ],
            Rules = new SwissMortgageRuleSettings()
        };

        Assert.Equal(1_100_000m, scenario.TotalPurchasePrice);
        Assert.Equal(200_000m, scenario.TotalGrossAnnualIncome);
        Assert.Equal(150_000m, scenario.CashEquity);
        Assert.Equal(50_000m, scenario.Pillar2BvgEquity);
        Assert.Equal(200_000m, scenario.TotalEquity);
    }
}