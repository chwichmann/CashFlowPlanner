namespace CashFlowPlanner.Core.Accounts;

public sealed class Account
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public List<AccountInterestContract> InterestContracts { get; init; } = [];

    public required string Name { get; init; }

    public AccountType Type { get; init; } = AccountType.BankAccount;

    public string Currency { get; init; } = "CHF";

    public decimal OpeningBalance { get; init; }

    public DateOnly OpeningDate { get; init; }

    public bool IsActive { get; init; } = true;

    public string? BankName { get; init; }

    public string? IbanMasked { get; init; }

    public string? Notes { get; init; }

    public List<AccountOwner> Owners { get; set; } = new();

    public Pillar3aAccountSubtype? Pillar3aSubtype { get; set; }

    public bool IsLiability =>
        Type is AccountType.CreditCard
            or AccountType.Mortgage
            or AccountType.Loan;

    public override string ToString()
        => $"{Name} ({Type})";
}