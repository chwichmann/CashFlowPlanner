using CashFlowPlanner.Core;
using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.Storage.Json.Tests;

public sealed class CashFlowPlanJsonSerializerTests
{
    [Fact]
    public void SerializePlan_Should_ProduceReadableJson()
    {
        // Arrange
        var plan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(plan);

        // Assert
        Assert.Contains("\"schemaVersion\": 1", json);
        Assert.Contains("\"name\": \"Private Cashflow\"", json);
        Assert.Contains("\"baseCurrency\": \"CHF\"", json);

        Assert.Contains("\"type\": \"BankAccount\"", json);
        Assert.Contains("\"kind\": \"ExternalIncome\"", json);
        Assert.Contains("\"frequency\": \"Monthly\"", json);
        Assert.Contains("\"paymentMethod\": \"Lsv\"", json);

        Assert.Contains("\"mortgages\":", json);
        Assert.Contains("\"name\": \"House SARON Mortgage\"", json);
        Assert.Contains("\"type\": \"Saron\"", json);
        Assert.Contains("\"amortisationMode\": \"Direct\"", json);
        Assert.Contains("\"paymentInterval\": \"Quarterly\"", json);
        Assert.Contains("\"billingCalendar\": \"BankQuarters\"", json);
        Assert.Contains("\"fixedInterestPercent\": 0.65", json);
        Assert.Contains("\"ratePercent\": 1.2", json);

        Assert.DoesNotContain("\"type\": 0", json);
        Assert.DoesNotContain("\"frequency\": 3", json);

        Assert.Contains("\"persons\":", json);
        Assert.Contains("\"displayName\": \"Christian\"", json);
        Assert.Contains("\"pillar3aEligibility\": \"WithPensionFund\"", json);
        Assert.Contains("\"annualEarnedIncome\": 120000", json);

        Assert.Contains("\"owners\":", json);
        Assert.Contains("\"ownershipShare\": 1", json);

        Assert.Contains("\"type\": \"Pillar3a\"", json);
        Assert.Contains("\"pillar3aSubtype\": \"FundSolution\"", json);

        Assert.Contains("\"incomePersonId\": \"01000000-0000-0000-0000-000000000001\"", json);
        Assert.Contains("\"name\": \"Pillar 3a Contribution\"", json);

        Assert.DoesNotContain("\"pillar3aSubtype\": 1", json);
        Assert.DoesNotContain("\"pillar3aEligibility\": 0", json);

        Assert.Contains("\"interestContracts\":", json);
        Assert.Contains("\"name\": \"Savings Interest\"", json);
        Assert.Contains("\"calculationMethod\": \"TieredBalance\"", json);
        Assert.Contains("\"postingFrequency\": \"Yearly\"", json);
        Assert.Contains("\"dayCountConvention\": \"Actual360\"", json);
        Assert.Contains("\"annualRatePercent\": 2", json);
        Assert.DoesNotContain("\"dayCountConvention\": 0", json);

        Assert.Contains("\"defaultPaymentAccountId\": \"10000000-0000-0000-0000-000000000001\"", json);
        Assert.Contains("\"treatWeekendsAsBankOffDays\": true", json);
        Assert.Contains("\"bankOffDays\":", json);
        Assert.Contains("\"date\": \"2026-08-03\"", json);
        Assert.Contains("\"name\": \"Bank holiday\"", json);
        Assert.Contains("\"note\": \"Storage roundtrip test\"", json);
    }

    [Fact]
    public void DeserializePlan_Should_LoadSerializedPlan()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var json = serializer.SerializePlan(originalPlan);

        // Act
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        Assert.Equal(originalPlan.Id, loadedPlan.Id);
        Assert.Equal(originalPlan.Name, loadedPlan.Name);
        Assert.Equal(originalPlan.BaseCurrency, loadedPlan.BaseCurrency);

        Assert.Equal(originalPlan.Accounts.Count, loadedPlan.Accounts.Count);
        Assert.Equal(originalPlan.Transactions.Count, loadedPlan.Transactions.Count);

        Assert.Equal(
            originalPlan.SimulationSettings.StartDate,
            loadedPlan.SimulationSettings.StartDate);

        Assert.Equal(
            originalPlan.SimulationSettings.EndDate,
            loadedPlan.SimulationSettings.EndDate);

        Assert.Equal(originalPlan.Persons.Count, loadedPlan.Persons.Count);
        Assert.Equal(originalPlan.Mortgages.Count, loadedPlan.Mortgages.Count);
        Assert.Equal(originalPlan.CreditCards.Count, loadedPlan.CreditCards.Count);
    }

    [Fact]
    public void Roundtrip_Should_PreserveAccounts()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        var originalMainAccount = originalPlan.Accounts.Single(x => x.Name == "Main Account");
        var loadedMainAccount = loadedPlan.Accounts.Single(x => x.Name == "Main Account");

        Assert.Equal(originalMainAccount.Id, loadedMainAccount.Id);
        Assert.Equal(originalMainAccount.Type, loadedMainAccount.Type);
        Assert.Equal(originalMainAccount.Currency, loadedMainAccount.Currency);
        Assert.Equal(originalMainAccount.OpeningBalance, loadedMainAccount.OpeningBalance);
        Assert.Equal(originalMainAccount.OpeningDate, loadedMainAccount.OpeningDate);
        Assert.Equal(originalMainAccount.BankName, loadedMainAccount.BankName);
    }

    [Fact]
    public void Roundtrip_Should_PreserveTransactionsAndSchedules()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        var originalLeasing = originalPlan.Transactions.Single(x => x.Name == "Car Leasing");
        var loadedLeasing = loadedPlan.Transactions.Single(x => x.Name == "Car Leasing");

        Assert.Equal(originalLeasing.Id, loadedLeasing.Id);
        Assert.Equal(originalLeasing.Kind, loadedLeasing.Kind);
        Assert.Equal(originalLeasing.FromAccountId, loadedLeasing.FromAccountId);
        Assert.Equal(originalLeasing.ToAccountId, loadedLeasing.ToAccountId);
        Assert.Equal(originalLeasing.Amount, loadedLeasing.Amount);
        Assert.Equal(originalLeasing.Currency, loadedLeasing.Currency);
        Assert.Equal(originalLeasing.PaymentMethod, loadedLeasing.PaymentMethod);
        Assert.Equal(originalLeasing.Priority, loadedLeasing.Priority);

        Assert.Equal(originalLeasing.Schedule.Frequency, loadedLeasing.Schedule.Frequency);
        Assert.Equal(originalLeasing.Schedule.StartDate, loadedLeasing.Schedule.StartDate);
        Assert.Equal(originalLeasing.Schedule.EndDate, loadedLeasing.Schedule.EndDate);
        Assert.Equal(originalLeasing.Schedule.DayOfMonth, loadedLeasing.Schedule.DayOfMonth);
        Assert.Equal(
            originalLeasing.Schedule.BusinessDayAdjustment,
            loadedLeasing.Schedule.BusinessDayAdjustment);

        Assert.Equal(
            originalPlan.DefaultPaymentAccountId,
            loadedPlan.DefaultPaymentAccountId);

        Assert.Equal(
            originalPlan.TreatWeekendsAsBankOffDays,
            loadedPlan.TreatWeekendsAsBankOffDays);

        Assert.Equal(
            originalPlan.BankOffDays.Count,
            loadedPlan.BankOffDays.Count);
    }

    [Fact]
    public void DeserializeDocument_Should_RejectUnsupportedSchemaVersion()
    {
        // Arrange
        var json = """
        {
          "schemaVersion": 999,
          "planId": "00000000-0000-0000-0000-000000000001",
          "name": "Private Cashflow",
          "baseCurrency": "CHF",
          "createdAt": "2026-06-01T00:00:00Z",
          "modifiedAt": "2026-06-01T00:00:00Z",
          "accounts": [],
          "transactions": [],
          "simulationSettings": {
            "startDate": "2026-06-01",
            "endDate": "2026-12-31",
            "granularity": "Daily",
            "includeInactiveAccounts": false,
            "warnOnNegativeBankBalance": true
          }
        }
        """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<NotSupportedException>(
            () => serializer.DeserializeDocument(json));

        // Assert
        Assert.Contains("Unsupported cashflow plan schema version", exception.Message);
    }

    [Fact]
    public void DeserializePlan_Should_RejectEmptyJson()
    {
        // Arrange
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.DeserializePlan(""));

        // Assert
        Assert.Equal("JSON content is empty.", exception.Message);
    }

    [Fact]
    public void DeserializePlan_Should_RejectInvalidPlan()
    {
        // Arrange
        var json = """
    {
      "schemaVersion": 1,
      "planId": "00000000-0000-0000-0000-000000000001",
      "name": "",
      "baseCurrency": "CHF",
      "createdAt": "2026-06-01T00:00:00Z",
      "modifiedAt": "2026-06-01T00:00:00Z",
      "persons": [],
      "accounts": [],
      "transactions": [],
      "mortgages": [],
      "creditCards": [],
      "pillar3aContracts": [],
      "simulationSettings": {
        "dateMode": "ExplicitDateRange",
        "startDate": "2026-06-01",
        "endDate": "2026-12-31",
        "granularity": "Daily",
        "includeInactiveAccounts": false,
        "warnOnNegativeBankBalance": true
      }
    }
    """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var exception = Assert.Throws<InvalidOperationException>(
            () => serializer.DeserializePlan(json));

        // Assert
        Assert.Contains(
            "name is required",
            exception.Message,
            StringComparison.OrdinalIgnoreCase);
    }


    [Fact]
    public async Task DeserializePlanAsync_Should_LoadPlanFromStream()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();
        var json = serializer.SerializePlan(originalPlan);

        await using var stream = new MemoryStream();
        await using var writer = new StreamWriter(stream, leaveOpen: true);

        await writer.WriteAsync(json);
        await writer.FlushAsync();

        stream.Position = 0;

        // Act
        var loadedPlan = await serializer.DeserializePlanAsync(stream);

        // Assert
        Assert.Equal(originalPlan.Id, loadedPlan.Id);
        Assert.Equal(originalPlan.Name, loadedPlan.Name);
        Assert.Equal(originalPlan.Accounts.Count, loadedPlan.Accounts.Count);
        Assert.Equal(originalPlan.Transactions.Count, loadedPlan.Transactions.Count);
    }

    [Fact]
    public void DeserializedPlan_Should_BeSimulatable()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        var engine = new SimulationEngine();

        // Act
        var result = engine.Simulate(loadedPlan);

        // Assert
        Assert.NotEmpty(result.Events);
        Assert.NotEmpty(result.BalancePoints);
    }

    [Fact]
    public void Roundtrip_Should_PreserveMortgagesAndSaronRates()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        Assert.Single(loadedPlan.Mortgages);

        var originalMortgage = originalPlan.Mortgages.Single();
        var loadedMortgage = loadedPlan.Mortgages.Single();

        Assert.Equal(originalMortgage.Id, loadedMortgage.Id);
        Assert.Equal(originalMortgage.Name, loadedMortgage.Name);
        Assert.Equal(originalMortgage.Type, loadedMortgage.Type);
        Assert.Equal(originalMortgage.PaymentAccountId, loadedMortgage.PaymentAccountId);
        Assert.Equal(originalMortgage.InitialPrincipal, loadedMortgage.InitialPrincipal);
        Assert.Equal(originalMortgage.InitialDate, loadedMortgage.InitialDate);
        Assert.Equal(originalMortgage.CalculationPrincipal, loadedMortgage.CalculationPrincipal);
        Assert.Equal(originalMortgage.CalculationPrincipalDate, loadedMortgage.CalculationPrincipalDate);
        Assert.Equal(originalMortgage.FixedInterestPercent, loadedMortgage.FixedInterestPercent);
        Assert.Equal(originalMortgage.AmortisationMode, loadedMortgage.AmortisationMode);
        Assert.Equal(originalMortgage.AnnualAmortisationAmount, loadedMortgage.AnnualAmortisationAmount);
        Assert.Equal(originalMortgage.PaymentInterval, loadedMortgage.PaymentInterval);
        Assert.Equal(originalMortgage.BillingCalendar, loadedMortgage.BillingCalendar);
        Assert.Equal(originalMortgage.IsActive, loadedMortgage.IsActive);

        Assert.Equal(originalMortgage.SaronRates.Count, loadedMortgage.SaronRates.Count);

        for (var i = 0; i < originalMortgage.SaronRates.Count; i++)
        {
            Assert.Equal(originalMortgage.SaronRates[i].Date, loadedMortgage.SaronRates[i].Date);
            Assert.Equal(originalMortgage.SaronRates[i].RatePercent, loadedMortgage.SaronRates[i].RatePercent);
        }
    }

    [Fact]
    public void Roundtrip_Should_PreserveCreditCardContracts()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        Assert.Single(loadedPlan.CreditCards);

        var original = originalPlan.CreditCards.Single();
        var loaded = loadedPlan.CreditCards.Single();

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.CreditCardAccountId, loaded.CreditCardAccountId);
        Assert.Equal(original.PaymentAccountId, loaded.PaymentAccountId);
        Assert.Equal(original.ClosingDayOfMonth, loaded.ClosingDayOfMonth);
        Assert.Equal(original.PaymentDayOfMonth, loaded.PaymentDayOfMonth);
        Assert.Equal(original.PaymentMethod, loaded.PaymentMethod);
        Assert.Equal(original.PaymentBusinessDayAdjustment, loaded.PaymentBusinessDayAdjustment);
        Assert.Equal(original.StartDate, loaded.StartDate);
        Assert.Equal(original.EndDate, loaded.EndDate);
        Assert.Equal(original.IsActive, loaded.IsActive);
    }

    [Fact]
    public void Roundtrip_Should_PreservePersons()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        Assert.Equal(originalPlan.Persons.Count, loadedPlan.Persons.Count);

        var originalChristian = originalPlan.Persons.Single(x => x.DisplayName == "Christian");
        var loadedChristian = loadedPlan.Persons.Single(x => x.DisplayName == "Christian");

        Assert.Equal(originalChristian.Id, loadedChristian.Id);
        Assert.Equal(originalChristian.DisplayName, loadedChristian.DisplayName);
        Assert.Equal(originalChristian.DateOfBirth, loadedChristian.DateOfBirth);
        Assert.Equal(originalChristian.Pillar3aEligibility, loadedChristian.Pillar3aEligibility);
        Assert.Equal(originalChristian.AnnualEarnedIncome, loadedChristian.AnnualEarnedIncome);

        var originalPartner = originalPlan.Persons.Single(x => x.DisplayName == "Partner");
        var loadedPartner = loadedPlan.Persons.Single(x => x.DisplayName == "Partner");

        Assert.Equal(originalPartner.Id, loadedPartner.Id);
        Assert.Equal(originalPartner.DisplayName, loadedPartner.DisplayName);
        Assert.Equal(originalPartner.DateOfBirth, loadedPartner.DateOfBirth);
        Assert.Equal(originalPartner.Pillar3aEligibility, loadedPartner.Pillar3aEligibility);
        Assert.Equal(originalPartner.AnnualEarnedIncome, loadedPartner.AnnualEarnedIncome);
    }

    [Fact]
    public void Roundtrip_Should_PreserveAccountOwnersAndPillar3aSubtype()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        var originalPillar3a = originalPlan.Accounts.Single(x => x.Name == "Pillar 3a");
        var loadedPillar3a = loadedPlan.Accounts.Single(x => x.Name == "Pillar 3a");

        Assert.Equal(AccountType.Pillar3a, loadedPillar3a.Type);
        Assert.Equal(originalPillar3a.Pillar3aSubtype, loadedPillar3a.Pillar3aSubtype);

        Assert.Single(loadedPillar3a.Owners);
        Assert.Equal(originalPillar3a.Owners.Single().PersonId, loadedPillar3a.Owners.Single().PersonId);
        Assert.Equal(originalPillar3a.Owners.Single().OwnershipShare, loadedPillar3a.Owners.Single().OwnershipShare);

        var originalSavings = originalPlan.Accounts.Single(x => x.Name == "Savings Account");
        var loadedSavings = loadedPlan.Accounts.Single(x => x.Name == "Savings Account");

        Assert.Equal(2, loadedSavings.Owners.Count);
        Assert.Equal(
            originalSavings.Owners.OrderBy(x => x.PersonId).Select(x => x.PersonId),
            loadedSavings.Owners.OrderBy(x => x.PersonId).Select(x => x.PersonId));
        Assert.Equal(
            originalSavings.Owners.OrderBy(x => x.PersonId).Select(x => x.OwnershipShare),
            loadedSavings.Owners.OrderBy(x => x.PersonId).Select(x => x.OwnershipShare));
    }

    [Fact]
    public void Roundtrip_Should_PreserveTransactionIncomePerson()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        var originalSalary = originalPlan.Transactions.Single(x => x.Name == "Salary");
        var loadedSalary = loadedPlan.Transactions.Single(x => x.Name == "Salary");

        Assert.Equal(originalSalary.IncomePersonId, loadedSalary.IncomePersonId);

        var originalPillar3aContribution = originalPlan.Transactions.Single(x => x.Name == "Pillar 3a Contribution");
        var loadedPillar3aContribution = loadedPlan.Transactions.Single(x => x.Name == "Pillar 3a Contribution");

        Assert.Equal(originalPillar3aContribution.IncomePersonId, loadedPillar3aContribution.IncomePersonId);
        Assert.Null(loadedPillar3aContribution.IncomePersonId);
        Assert.Equal(originalPillar3aContribution.ToAccountId, loadedPillar3aContribution.ToAccountId);
    }

    [Fact]
    public void Roundtrip_Should_PreserveAccountInterestContracts()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        // Assert
        var originalSavings = originalPlan.Accounts.Single(x => x.Name == "Savings Account");
        var loadedSavings = loadedPlan.Accounts.Single(x => x.Name == "Savings Account");

        Assert.Single(loadedSavings.InterestContracts);

        var originalContract = originalSavings.InterestContracts.Single();
        var loadedContract = loadedSavings.InterestContracts.Single();

        Assert.Equal(originalContract.Id, loadedContract.Id);
        Assert.Equal(originalContract.Name, loadedContract.Name);
        Assert.Equal(originalContract.CalculationMethod, loadedContract.CalculationMethod);
        Assert.Equal(originalContract.PostingFrequency, loadedContract.PostingFrequency);
        Assert.Equal(originalContract.DayCountConvention, loadedContract.DayCountConvention);
        Assert.Equal(originalContract.StartDate, loadedContract.StartDate);
        Assert.Equal(originalContract.EndDate, loadedContract.EndDate);
        Assert.Equal(originalContract.IsActive, loadedContract.IsActive);

        Assert.Equal(originalContract.Tiers.Count, loadedContract.Tiers.Count);

        for (var i = 0; i < originalContract.Tiers.Count; i++)
        {
            Assert.Equal(originalContract.Tiers[i].FromAmount, loadedContract.Tiers[i].FromAmount);
            Assert.Equal(originalContract.Tiers[i].ToAmount, loadedContract.Tiers[i].ToAmount);
            Assert.Equal(originalContract.Tiers[i].AnnualRatePercent, loadedContract.Tiers[i].AnnualRatePercent);
        }
    }

    [Fact]
    public void Roundtrip_Should_PreservePillar3aContracts()
    {
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        Assert.Equal(originalPlan.Pillar3aContracts.Count, loadedPlan.Pillar3aContracts.Count);

        var original = originalPlan.Pillar3aContracts.Single();
        var loaded = loadedPlan.Pillar3aContracts.Single();

        Assert.Equal(original.Id, loaded.Id);
        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.OwnerPersonId, loaded.OwnerPersonId);
        Assert.Equal(original.Type, loaded.Type);
        Assert.Equal(original.OpeningValue, loaded.OpeningValue);
        Assert.Equal(original.OpeningDate, loaded.OpeningDate);
        Assert.Equal(original.Currency, loaded.Currency);
        Assert.Equal(original.ProviderName, loaded.ProviderName);
        Assert.Equal(original.ProjectionAssumption.Method, loaded.ProjectionAssumption.Method);
        Assert.Equal(original.ProjectionAssumption.ExpectedAnnualReturnPercent, loaded.ProjectionAssumption.ExpectedAnnualReturnPercent);
        Assert.Equal(original.ProjectionAssumption.AnnualFeePercent, loaded.ProjectionAssumption.AnnualFeePercent);
        Assert.Equal(original.ContributionSchedules.Count, loaded.ContributionSchedules.Count);
    }

    [Fact]
    public void Roundtrip_Should_Preserve_HouseBuyScenarios()
    {
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();

        var serializer = new CashFlowPlanJsonSerializer();

        var json = serializer.SerializePlan(originalPlan);
        var loadedPlan = serializer.DeserializePlan(json);

        var original = originalPlan.HouseBuyScenarios.Single();
        var loaded = loadedPlan.HouseBuyScenarios.Single();

        Assert.Equal(original.Name, loaded.Name);
        Assert.Equal(original.SalePrice, loaded.SalePrice);
        Assert.Equal(original.SaleRemainingMortgage, loaded.SaleRemainingMortgage);
        Assert.Equal(original.BuyPrice, loaded.BuyPrice);
        Assert.Equal(original.DesiredMortgage, loaded.DesiredMortgage);

        Assert.Single(loaded.Persons);
        Assert.Single(loaded.EquitySources);
    }

    [Fact]
    public void Roundtrip_Should_PreservePlanDefaultsAndBankCalendar()
    {
        // Arrange
        var originalPlan = StorageTestPlanFactory.CreateSimplePlan();
        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var json = serializer.SerializePlan(originalPlan);
        var restored = serializer.DeserializePlan(json);

        // Assert
        Assert.Equal(
            originalPlan.DefaultPaymentAccountId,
            restored.DefaultPaymentAccountId);

        Assert.Equal(
            originalPlan.TreatWeekendsAsBankOffDays,
            restored.TreatWeekendsAsBankOffDays);

        Assert.Equal(
            originalPlan.BankOffDays.Count,
            restored.BankOffDays.Count);

        var originalBankOffDay = originalPlan.BankOffDays.Single();
        var restoredBankOffDay = restored.BankOffDays.Single();

        Assert.Equal(originalBankOffDay.Date, restoredBankOffDay.Date);
        Assert.Equal(originalBankOffDay.Name, restoredBankOffDay.Name);
        Assert.Equal(originalBankOffDay.Note, restoredBankOffDay.Note);
    }

    [Fact]
    public void DeserializePlan_WithoutPlanDefaultsAndBankCalendar_Should_UseDefaults()
    {
        // Arrange
        var json = """
    {
      "schemaVersion": 1,
      "planId": "00000000-0000-0000-0000-000000000001",
      "name": "Private Cashflow",
      "baseCurrency": "CHF",
      "persons": [],
      "accounts": [],
      "transactions": [],
      "mortgages": [],
      "creditCards": [],
      "pillar3aContracts": [],
      "houseBuyScenarios": [],
      "simulationSettings": {
        "dateMode": "ExplicitDateRange",
        "startDate": "2026-06-01",
        "endDate": "2026-12-31",
        "granularity": "Daily",
        "includeInactiveAccounts": false,
        "warnOnNegativeBankBalance": true
      }
    }
    """;

        var serializer = new CashFlowPlanJsonSerializer();

        // Act
        var plan = serializer.DeserializePlan(json);

        // Assert
        Assert.Null(plan.DefaultPaymentAccountId);
        Assert.True(plan.TreatWeekendsAsBankOffDays);
        Assert.Empty(plan.BankOffDays);
    }
}