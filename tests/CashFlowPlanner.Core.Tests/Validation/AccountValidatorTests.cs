using CashFlowPlanner.Core.Accounts;
using CashFlowPlanner.Core.People;
using CashFlowPlanner.Core.Validation;

namespace CashFlowPlanner.Core.Tests.Validation;

public sealed class AccountValidatorTests
{
    [Fact]
    public void Pillar3aAccount_WithOneOwnerAndSubtype_IsValid()
    {
        var person = new Person
        {
            Id = Guid.NewGuid(),
            DisplayName = "Christian"
        };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "3a VIAC Global 100",
            Type = AccountType.Pillar3a,
            Pillar3aSubtype = Pillar3aAccountSubtype.FundSolution,
            Owners =
            {
                new AccountOwner
                {
                    PersonId = person.Id,
                    OwnershipShare = 1m
                }
            }
        };

        var result = AccountValidator.Validate(
            new[] { account },
            new[] { person });

        Assert.Empty(result);
    }

    [Fact]
    public void Pillar3aAccount_WithoutOwner_ReturnsError()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "3a Account",
            Type = AccountType.Pillar3a,
            Pillar3aSubtype = Pillar3aAccountSubtype.BankAccount
        };

        var result = AccountValidator.Validate(
            new[] { account },
            Array.Empty<Person>());

        Assert.Contains(result, x =>
            x.Code == "Pillar3aAccount.InvalidOwnerCount" &&
            x.Severity == PlanValidationSeverity.Error);
    }

    [Fact]
    public void Pillar3aAccount_WithTwoOwners_ReturnsError()
    {
        var person1 = new Person { Id = Guid.NewGuid(), DisplayName = "Person 1" };
        var person2 = new Person { Id = Guid.NewGuid(), DisplayName = "Person 2" };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Joint 3a Account",
            Type = AccountType.Pillar3a,
            Pillar3aSubtype = Pillar3aAccountSubtype.BankAccount,
            Owners =
            {
                new AccountOwner { PersonId = person1.Id, OwnershipShare = 0.5m },
                new AccountOwner { PersonId = person2.Id, OwnershipShare = 0.5m }
            }
        };

        var result = AccountValidator.Validate(
            new[] { account },
            new[] { person1, person2 });

        Assert.Contains(result, x =>
            x.Code == "Pillar3aAccount.InvalidOwnerCount");
    }

    [Fact]
    public void Pillar3aAccount_WithoutSubtype_ReturnsError()
    {
        var person = new Person { Id = Guid.NewGuid(), DisplayName = "Christian" };

        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "3a Account",
            Type = AccountType.Pillar3a,
            Owners =
            {
                new AccountOwner
                {
                    PersonId = person.Id,
                    OwnershipShare = 1m
                }
            }
        };

        var result = AccountValidator.Validate(
            new[] { account },
            new[] { person });

        Assert.Contains(result, x =>
            x.Code == "Pillar3aAccount.MissingSubtype");
    }

    [Fact]
    public void Account_WithUnknownOwner_ReturnsError()
    {
        var account = new Account
        {
            Id = Guid.NewGuid(),
            Name = "Savings",
            Type = AccountType.BankAccount,
            Owners =
            {
                new AccountOwner
                {
                    PersonId = Guid.NewGuid(),
                    OwnershipShare = 1m
                }
            }
        };

        var result = AccountValidator.Validate(
            new[] { account },
            Array.Empty<Person>());

        Assert.Contains(result, x =>
            x.Code == "AccountOwner.PersonNotFound");
    }
}
