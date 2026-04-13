namespace GestorOT.Domain.Entities;

public class UserProfile : TenantEntity
{
    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = "Agronomist";
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
}
