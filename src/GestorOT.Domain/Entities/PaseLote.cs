namespace GestorOT.Domain.Entities;

public class PaseLote : TenantEntity
{
    public DateTime GeneradoEn { get; set; } = DateTime.UtcNow;
    public string? Descripcion { get; set; }
    public int TotalPases { get; set; }
    public string Estado { get; set; } = "Generado";

    public ICollection<PaseImputacion> Pases { get; set; } = new List<PaseImputacion>();
}
