namespace CashFlowPlanner.Core.Accounts;

public static class AccountBankIdentifierMatcher
{
    public static Account? FindByIdentifier(
        IEnumerable<Account> accounts,
        AccountBankIdentifierType identifierType,
        string identifierValue,
        string? bankName = null)
    {
        var normalizedIdentifierValue = AccountBankIdentifier.Normalize(identifierValue);

        return accounts.SingleOrDefault(account =>
            HasMatchingIdentifier(
                account,
                identifierType,
                normalizedIdentifierValue,
                bankName));
    }

    public static bool HasIdentifier(
        Account account,
        AccountBankIdentifierType identifierType,
        string identifierValue,
        string? bankName = null)
    {
        var normalizedIdentifierValue = AccountBankIdentifier.Normalize(identifierValue);

        return HasMatchingIdentifier(
            account,
            identifierType,
            normalizedIdentifierValue,
            bankName);
    }

    public static bool HasMt940AccountId(
        Account account,
        string mt940AccountId,
        string? bankName = null)
    {
        return HasIdentifier(
            account,
            AccountBankIdentifierType.Mt940AccountId,
            mt940AccountId,
            bankName);
    }

    public static Account? FindByMt940AccountId(
        IEnumerable<Account> accounts,
        string mt940AccountId,
        string? bankName = null)
    {
        return FindByIdentifier(
            accounts,
            AccountBankIdentifierType.Mt940AccountId,
            mt940AccountId,
            bankName);
    }

    public static Account WithAddedIdentifierIfMissing(
        Account account,
        AccountBankIdentifier identifier)
    {
        if (HasIdentifier(
                account,
                identifier.Type,
                identifier.Value,
                identifier.BankName))
        {
            return account;
        }

        var identifiers = account.BankIdentifiers
            .Concat([identifier])
            .ToList();

        return CopyAccount(
            account,
            identifiers);
    }

    private static bool HasMatchingIdentifier(
        Account account,
        AccountBankIdentifierType identifierType,
        string normalizedIdentifierValue,
        string? bankName)
    {
        return account.BankIdentifiers.Any(identifier =>
            identifier.Type == identifierType &&
            string.Equals(
                identifier.NormalizedValue,
                normalizedIdentifierValue,
                StringComparison.OrdinalIgnoreCase) &&
            BankNameMatches(identifier.BankName, bankName));
    }

    private static bool BankNameMatches(
        string? identifierBankName,
        string? requestedBankName)
    {
        if (string.IsNullOrWhiteSpace(requestedBankName))
        {
            return true;
        }

        if (string.IsNullOrWhiteSpace(identifierBankName))
        {
            return true;
        }

        return string.Equals(
            identifierBankName.Trim(),
            requestedBankName.Trim(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Account CopyAccount(
        Account account,
        List<AccountBankIdentifier> bankIdentifiers)
    {
        return new Account
        {
            Id = account.Id,
            InterestContracts = account.InterestContracts,
            Name = account.Name,
            Type = account.Type,
            Currency = account.Currency,
            OpeningBalance = account.OpeningBalance,
            OpeningDate = account.OpeningDate,
            IsActive = account.IsActive,
            BankName = account.BankName,
            Iban = account.Iban,
            IbanMasked = account.IbanMasked,
            BankIdentifiers = bankIdentifiers,
            Notes = account.Notes,
            Owners = account.Owners,
            Pillar3aSubtype = account.Pillar3aSubtype
        };
    }
}