namespace CashFlowPlanner.BlazorWasm.Services;

/// <summary>What the user is being asked for, which changes what the dialog demands.</summary>
public enum PassphrasePromptMode
{
    /// <summary>Opening an existing encrypted file: one field.</summary>
    Unlock = 0,

    /// <summary>
    /// Choosing a passphrase for the first time: entered twice, plus an explicit
    /// acknowledgement that a forgotten passphrase cannot be recovered.
    /// </summary>
    Create = 1
}

public sealed record PassphraseRequest(
    PassphrasePromptMode Mode,
    string? FileName,
    string? ErrorMessage);

/// <summary>
/// Lets any code ask for a passphrase and await the answer, without every caller
/// having to host its own dialog. One <c>PassphraseDialog</c> in the layout listens
/// and completes the request. Same shape as <see cref="UiFeedbackService"/>.
/// </summary>
public sealed class PassphrasePromptService
{
    private TaskCompletionSource<string?>? _pending;

    public event Action? Changed;

    public PassphraseRequest? Current { get; private set; }

    public bool IsPrompting => Current is not null;

    /// <summary>
    /// Ask for a passphrase. Returns null if the user cancels.
    /// </summary>
    public Task<string?> RequestAsync(
        PassphrasePromptMode mode,
        string? fileName = null,
        string? errorMessage = null)
    {
        // A second request while one is open would strand the first caller awaiting a
        // task nobody completes. Cancel the old one rather than deadlock it.
        _pending?.TrySetResult(null);

        _pending = new TaskCompletionSource<string?>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        Current = new PassphraseRequest(mode, fileName, errorMessage);
        Changed?.Invoke();

        return _pending.Task;
    }

    /// <summary>Called by the dialog when the user confirms.</summary>
    public void Complete(string passphrase)
    {
        var pending = _pending;

        Current = null;
        _pending = null;
        Changed?.Invoke();

        pending?.TrySetResult(passphrase);
    }

    /// <summary>Called by the dialog when the user cancels or dismisses.</summary>
    public void Cancel()
    {
        var pending = _pending;

        Current = null;
        _pending = null;
        Changed?.Invoke();

        pending?.TrySetResult(null);
    }
}
