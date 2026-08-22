using CashFlowPlanner.Core.Pillar3a;

namespace CashFlowPlanner.BlazorWasm.Models;

/// <summary>
/// One planned withdrawal from a Pillar 3a contract.
///
/// These were persisted, validated and - since wave 4 - simulated, but the contract editor never
/// offered them, and worse, it wrote <c>Withdrawals = []</c> on every save. Opening a contract that
/// carried withdrawals and pressing Save deleted them without a word.
/// </summary>
public sealed class Pillar3aWithdrawalEditModel
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public Pillar3aWithdrawalReason Reason { get; set; } = Pillar3aWithdrawalReason.Retirement;

    /// <summary>
    /// Null means "whatever is left", which is only meaningful together with
    /// <see cref="CloseContract"/> - Core rejects the pair otherwise.
    /// </summary>
    public decimal? Amount { get; set; }

    public bool CloseContract { get; set; }

    public Guid? TargetAccountId { get; set; }

    public string? Notes { get; set; }

    public static Pillar3aWithdrawalEditModel FromWithdrawal(Pillar3aWithdrawalEvent withdrawal)
    {
        ArgumentNullException.ThrowIfNull(withdrawal);

        return new Pillar3aWithdrawalEditModel
        {
            Id = withdrawal.Id,
            Date = withdrawal.Date,
            Reason = withdrawal.Reason,
            Amount = withdrawal.Amount,
            CloseContract = withdrawal.CloseContract,
            TargetAccountId = withdrawal.TargetAccountId,
            Notes = withdrawal.Notes
        };
    }

    public Pillar3aWithdrawalEvent ToWithdrawal()
    {
        return new Pillar3aWithdrawalEvent
        {
            Id = Id,
            Date = Date,
            Reason = Reason,
            Amount = Amount,
            CloseContract = CloseContract,
            TargetAccountId = TargetAccountId,
            Notes = string.IsNullOrWhiteSpace(Notes) ? null : Notes.Trim()
        };
    }
}
