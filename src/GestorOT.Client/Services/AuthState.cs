using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class AuthState
{
    public AuthUserInfoDto? User { get; private set; }
    public bool IsAuthenticated => User != null;
    public bool IsInitialized { get; set; }

    public event Action? OnChange;

    public void SetUser(AuthUserInfoDto? user)
    {
        User = user;
        IsInitialized = true;
        OnChange?.Invoke();
    }

    public void ClearUser()
    {
        User = null;
        IsInitialized = true;
        OnChange?.Invoke();
    }
}
