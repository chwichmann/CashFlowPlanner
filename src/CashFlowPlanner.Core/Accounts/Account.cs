namespace CashFlowPlanner.Core.Accounts;

public sealed class Account
{
    public Guid Id { get; init; } = Guid.NewGuid();

    public List<AccountInterestContract> InterestContracts { get; init; } = [];

    public required string Name { get; init; }

    public AccountType Type { get; init; } = AccountType.BankAccount;

    public string Currency { get; init; } = "CHF";

    /// <summary>
    /// Known account balance as of <see cref="OpeningDate"/>.
    /// UI wording should present this as "Current balance" / "Aktueller Saldo".
    /// </summary>
    public decimal OpeningBalance { get; init; }

    /// <summary>
    /// Date on which <see cref="OpeningBalance"/> is valid.
    /// UI wording should present this as "Balance date" / "Saldodatum".
    /// </summary>
    public DateOnly OpeningDate { get; init; }

    public bool IsActive { get; init; } = true;

    public string? BankName { get; init; }

    /// <summary>
    /// Optional full IBAN/account number.
    /// CashFlowPlanner already contains private financial planning data,
    /// so the account number can be stored directly if the user wants it.
    /// </summary>
    public string? Iban { get; init; }

    /// <summary>
    /// Optional display-safe IBAN/account number.
    /// Kept for existing UI compatibility.
    /// </summary>
    public string? IbanMasked { get; init; }

    /// <summary>
    /// External bank/import identifiers used for automatic account matching.
    /// Example: UBS MT940 tag :25: account identifier.
    /// </summary>
    public List<AccountBankIdentifier> BankIdentifiers { get; init; } = [];

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
