namespace CashFlowPlanner.BlazorWasm.Services;

public sealed class UiFeedbackService
{
    public UiNotification? CurrentNotification { get; private set; }

    public bool IsBusy { get; private set; }

    public string? BusyMessage { get; private set; }

    public event Action? Changed;

    public void Success(string message)
    {
        CurrentNotification = new UiNotification(
            UiNotificationKind.Success,
            message);

        NotifyChanged();
    }

    public void Info(string message)
    {
        CurrentNotification = new UiNotification(
            UiNotificationKind.Info,
            message);

        NotifyChanged();
    }

    public void Error(string message)
    {
        CurrentNotification = new UiNotification(
            UiNotificationKind.Error,
            message);

        NotifyChanged();
    }

    public void ClearNotification()
    {
        CurrentNotification = null;

        NotifyChanged();
    }

    public void StartBusy(string message)
    {
        IsBusy = true;
        BusyMessage = message;

        NotifyChanged();
    }

    public void StopBusy()
    {
        IsBusy = false;
        BusyMessage = null;

        NotifyChanged();
    }

    private void NotifyChanged()
    {
        Changed?.Invoke();
    }
}

public sealed record UiNotification(
    UiNotificationKind Kind,
    string Message);

public enum UiNotificationKind
{
    Success = 0,
    Info = 1,
    Error = 2
}