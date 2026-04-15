SSUE-00 · Entidad Rotación inexistente en el dominio
PRIORIDAD: CRITICO▶
Módulo Dominio · CampaignLot · Labor · AgronomicValidationService
Archivos
afectados
Domain/Entities/CampaignLot.cs · Domain/Entities/Labor.cs ·
CampaignLotConfiguration.cs · CampaignsController.cs · CampaignLotEditor.razor
Síntoma CampaignLot sólo tiene CropId (Guid?). No hay fechas de cultivo, no hay historial
de rotaciones por lote, ni lógica que las consuma.
Descripción
La rotación es la planificación del cultivo que tendrá un lote durante una campaña. Actualmente
CampaignLot sólo guarda un CropId suelto sin fechas. Esto impide:
• Saber qué se sembró/sembrará en un lote en una fecha dada
• Validar que la actividad de una Labor coincida con lo proyectado
• Notificar cuando la rotación supera el cierre de la campaña
• Construir historial agrónomico por lote
Fix — Capa de Dominio
Crear nueva entidad Rotation:
// Domain/Entities/Rotation.cs
public class Rotation : TenantEntity
{
public Guid CampaignLotId { get; set; }
public string CropName { get; set; } = string.Empty; // o FK a Crop
public DateOnly StartDate { get; set; }
public DateOnly EndDate { get; set; }
public string? Notes { get; set; }
public CampaignLot? CampaignLot { get; set; }
}
Agregar navegación desde CampaignLot:
public ICollection<Rotation> Rotations { get; set; } = new List<Rotation>();
Fix — Capa de Aplicación
Agregar interfaz de validación:
public interface IRotationService
{
Task<Rotation?> GetActiveRotationAsync(Guid campaignLotId, DateOnly date,
CancellationToken ct = default);
Task<List<RotationWarning>> ValidateRotationEndDatesAsync(Guid campaignId,
CancellationToken ct = default);
}
Fix — Validación de fecha de fin vs. cierre de campaña
En el service, al guardar una Rotation, emitir warning (no error) si EndDate > Campaign.EndDate:
if (rotation.EndDate > campaign.EndDate)
{
warnings.Add(new RotationWarning(
lotName, rotation.EndDate, campaign.EndDate,
"La rotación supera el cierre de la campaña."));
}
El warning se devuelve en el response (HTTP 200 con body que incluye Warnings[]). El frontend lo
muestra con un Alert de tipo Warning. No bloquea el guardado.
Fix — API
Nuevos endpoints bajo /api/campaigns/{id}/lots/{lotId}/rotations:
• GET — listar rotaciones del CampaignLot
• POST — crear rotación (devuelve warnings si aplica)
• PUT {rotationId} — editar
• DELETE {rotationId} — eliminar
Fix — Frontend (CampaignLotEditor.razor)
Agregar sección de Rotaciones en la tabla de lotes: al expandir una fila mostrar las rotaciones del lote
en esa campaña con fecha inicio, fin, cultivo y campo de notas. Mostrar badge de alerta si alguna
fecha de fin supera el EndDate de la campaña.
Impacto en ISSUE-10 (Validación de Actividad en Labor)
Una vez implementado, IRotationService.GetActiveRotationAsync se usa en LaboresSueltas para
preseleccionar actividad (ver ISSUE-10)

ISSUE-10 · Actividad en Labor no se valida contra la Rotación proyectada
PRIORIDAD: ALTO▶
Módulo Nueva Labor Suelta — Campo "Tipo de Actividad (ERP)"
Archivo LaboresSueltas.razor · LaborEditorForm.razor · AgronomicValidationService.cs
Síntoma El selector de actividad está siempre habilitado sin consultar si el lote tiene una
rotación proyectada para la fecha seleccionada. Depende de ISSUE-00 para
funcionar.
Flujo esperado
• El usuario selecciona Lote y Fecha Estimada
• El sistema consulta GET /api/campaigns/{id}/lots/{lotId}/rotations y evalúa si hay una rotación activa
en esa fecha
• Si hay rotación → preseleccionar el tipo de actividad asociado al cultivo de esa rotación y marcar el
campo como readonly con tooltip explicativo
• Si no hay rotación → habilitar selección manual y mostrar badge de advertencia: "Sin rotación
proyectada para esta fecha"
Fix — Frontend (LaborEditorForm.razor)
private async Task OnFechaEstimadaChanged(DateTime? fecha)
{
if (fecha == null || _model.CampaignLotId == null) return;
var dateOnly = DateOnly.FromDateTime(fecha.Value);
var rotacion = await Http.GetFromJsonAsync<RotationDto?>(
$"api/campaigns/{campaignId}/lots/{_model.CampaignLotId}/rotations/active?
date={dateOnly}");
if (rotacion != null)
{
_model.LaborTypeId = rotacion.SuggestedLaborTypeId;
_actividadLocked = true;
}
else
{
_actividadLocked = false;
_showNoRotacionWarning = true;
}
}
