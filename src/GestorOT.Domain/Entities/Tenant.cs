namespace GestorOT.Domain.Entities;

public class Tenant : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? GestorMaxApiKeyEncrypted { get; set; }
    public string? GestorMaxDatabaseId { get; set; }
    public DateTime CreatedAt { get; set; }
}
