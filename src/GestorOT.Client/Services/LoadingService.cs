namespace GestorOT.Client.Services;

public class LoadingService
{
    public bool IsLoading { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? SuccessMessage { get; private set; }

    public event Action? OnChange;

    public void Show()
    {
        IsLoading = true;
        // ErrorMessage = null; // Removed to prevent clearing on every show
        // SuccessMessage = null;
        OnChange?.Invoke();
    }

    public void Hide()
    {
        IsLoading = false;
        OnChange?.Invoke();
    }

    public void ShowError(string message)
    {
        ErrorMessage = message;
        IsLoading = false;
        OnChange?.Invoke();

        _ = Task.Run(async () =>
        {
            await Task.Delay(5000);
            ErrorMessage = null;
            OnChange?.Invoke();
        });
    }

    public void ShowSuccess(string message)
    {
        SuccessMessage = message;
        IsLoading = false;
        OnChange?.Invoke();

        _ = Task.Run(async () =>
        {
            await Task.Delay(3000);
            SuccessMessage = null;
            OnChange?.Invoke();
        });
    }

    public void Clear()
    {
        IsLoading = false;
        ErrorMessage = null;
        SuccessMessage = null;
        OnChange?.Invoke();
    }
}
