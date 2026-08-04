using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResponseDto> LoginAsync(LoginRequestDto request, CancellationToken ct = default);
    Task<AuthUserInfoDto?> GetCurrentUserInfoAsync(Guid userId, CancellationToken ct = default);
    void CreatePasswordHash(string password, out string passwordHash, out string passwordSalt);
    bool VerifyPasswordHash(string password, string passwordHash, string passwordSalt);
    string GenerateJwtToken(AuthUserInfoDto user);
}
