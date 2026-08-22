namespace CashFlowPlanner.BlazorWasm.Services.BankImport;

/// <summary>
/// The imported-statement store could not be written.
/// <para>
/// It exists because the failure is expected rather than exceptional: browser storage is about
/// 5 MB per origin, shared between the plan working copy and this store, and a multi-year CSV
/// import keeps the raw line of every transaction. The store filling up would otherwise be
/// silent - and worse than silent, because the next thing to be refused a write is the plan
/// autosave beside it.
/// </para>
/// <para>
/// Carries the browser's own error rather than a message, so the UI can say the right thing in
/// the right language and this service does not have to know either.
/// </para>
/// </summary>
public sealed class BankImportStorageException : Exception
{
    public BankImportStorageException(bool isQuotaExceeded, string? browserMessage)
        : base(browserMessage ?? "The imported statements could not be stored.")
    {
        IsQuotaExceeded = isQuotaExceeded;
        BrowserMessage = browserMessage;
    }

    /// <summary>True when the browser refused the write because storage is full.</summary>
    public bool IsQuotaExceeded { get; }

    public string? BrowserMessage { get; }
}
