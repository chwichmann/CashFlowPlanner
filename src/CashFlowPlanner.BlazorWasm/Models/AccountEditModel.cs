using CashFlowPlanner.Core.Accounts;

namespace CashFlowPlanner.BlazorWasm.Models;

public sealed class AccountEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public AccountType Type { get; set; } = AccountType.BankAccount;

    public string Currency { get; set; } = "CHF";

    public string? BankName { get; set; }

    public decimal OpeningBalance { get; set; }

    public DateOnly OpeningDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public string? IbanMasked { get; set; }

    public bool IsActive { get; set; } = true;

    public string? Notes { get; set; }

    public Guid? OwnerPersonId { get; set; }

    public Pillar3aAccountSubtype? Pillar3aSubtype { get; set; }

    public List<AccountInterestContractEditModel> InterestContracts { get; set; } = new();

    public static AccountEditModel FromAccount(Account account)
    {
        return new AccountEditModel
        {
            Id = account.Id,
            Name = account.Name,
            Type = account.Type,
            Currency = account.Currency,
            BankName = account.BankName,
            OpeningBalance = account.OpeningBalance,
            OpeningDate = account.OpeningDate,
            IbanMasked = account.IbanMasked,
            IsActive = account.IsActive,
            Notes = account.Notes,
            OwnerPersonId = account.Owners.FirstOrDefault()?.PersonId,
            Pillar3aSubtype = account.Pillar3aSubtype,
            InterestContracts = account.InterestContracts
                .Select(AccountInterestContractEditModel.FromContract)
                .ToList()
        };
    }

    public Account ToAccount()
    {
        return new Account
        {
            Id = Id,
            Name = Name.Trim(),
            Type = Type,
            Currency = Currency.Trim().ToUpperInvariant(),
            BankName = string.IsNullOrWhiteSpace(BankName) ? null : BankName.Trim(),
            OpeningBalance = OpeningBalance,
            OpeningDate = OpeningDate,
            IbanMasked = string.IsNullOrWhiteSpace(IbanMasked) ? null : IbanMasked.Trim(),
            IsActive = IsActive,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim(),

            Owners = OwnerPersonId is null
                ? new List<AccountOwner>()
                : new List<AccountOwner>
                {
                    new AccountOwner
                    {
                        PersonId = OwnerPersonId.Value,
                        OwnershipShare = 1m
                    }
                },

            Pillar3aSubtype = Type == AccountType.Pillar3a
                ? Pillar3aSubtype
                : null,

            InterestContracts = InterestContracts
                .Select(x => x.ToContract())
                .ToList()
        };
    }
}