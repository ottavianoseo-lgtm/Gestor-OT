namespace GestorOT.Domain.Entities;

/// <summary>
/// Imputacion de una regla contable para una actividad puntual. La regla fija todo lo comun
/// (empresa, comprobante, moneda, lista, centros); por actividad lo unico que rota son los
/// perfiles de imputacion, asi que se cargan aca en vez de duplicar la regla por cultivo.
/// Lo que la variante deja en null se toma de la regla.
/// </summary>
public class AccountConfigurationVariant : BaseEntity
{
    public Guid AccountConfigurationId { get; set; }
    public Guid ErpActivityId { get; set; }

    public long? CodPerfilDebe { get; set; }
    public long? CodPerfilHaber { get; set; }

    public AccountConfiguration? AccountConfiguration { get; set; }
    public ErpActivity? ErpActivity { get; set; }
}
