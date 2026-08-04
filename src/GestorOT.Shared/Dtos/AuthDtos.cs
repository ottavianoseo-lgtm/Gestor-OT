namespace GestorOT.Shared.Dtos;

public class LoginRequestDto
{
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;

    public LoginRequestDto() { }
    public LoginRequestDto(string email, string password)
    {
        Email = email;
        Password = password;
    }
}

public record LoginResponseDto(
    bool Success,
    string? Token,
    string? Message,
    AuthUserInfoDto? User
)
{
    public LoginResponseDto() : this(false, null, null, null) { }
}

public record AuthUserInfoDto(
    Guid UserId,
    string Email,
    string DisplayName,
    string Role,
    Guid TenantId,
    string TenantName
)
{
    public AuthUserInfoDto() : this(Guid.Empty, string.Empty, string.Empty, "Agronomist", Guid.Empty, string.Empty) { }
}

public class SetPasswordRequestDto
{
    public string NewPassword { get; set; } = string.Empty;

    public SetPasswordRequestDto() { }
    public SetPasswordRequestDto(string newPassword)
    {
        NewPassword = newPassword;
    }
}
