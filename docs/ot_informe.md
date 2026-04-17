GESTOR OT
Informe de Análisis de Brechas
Repositorio ot-refact vs. Documento Core OT (Stakeholder)
Abril 2026
1. Resumen Ejecutivo
Este informe contrasta el estado actual del repositorio GestorOT (rama ot-refact) con los requerimientos
funcionales definidos por el stakeholder principal en el documento Core OT. El objetivo es identificar las
brechas existentes y proporcionar una hoja de ruta clara para cerrarlas.
El repositorio demuestra una arquitectura sólida (Clean Architecture, multi-tenancy, EF Core con PostGIS) y
varias funcionalidades avanzadas ya implementadas. Sin embargo, existen brechas funcionales críticas en
la capa de presentación y en la lógica de negocio que impiden que el sistema satisfaga la visión del
stakeholder tal como está hoy.
Módulo / Entidad Estado actual Prioridad
Field (Campo) Implementado. HectareasTotales
en entidad debe eliminarse
Media
Lot (Lote) Implementado. Lógica N:N con
Campaña OK
Baja
CampaignLot Implementado. Falta polígono
GIS propio por relación
Media
Campaign – Bloqueo Parcialmente implementado.
Falta cascada a OT/Labor
Alta
Rotaciones Entidad existe. Validaciones de
solapamiento incompletas
Alta
Labor – Modo
Planeado/Realizado
Diferenciación solo por Status,
no por tipo
Alta
Labor – Modal creación Faltan campos: Tipo Labor,
advertencia Ha, adjuntos
Alta
LaborSupply – Hectáreas Sin campo Hectáreas propio por
insumo
Alta
WorkOrder – Estados Estados hardcodeados, sin tabla
configurable por usuario
Alta
WorkOrder – Vista detalle Sin sección Planeado/Realizado
con tablas de insumos
Crítica
Retiro Aprobado / Centro Sin implementación Alta
Insumos: Coef. Calculado Sin implementación de regla de
tres proporcional
Alta
Adjuntos de Labor/OT Sin implementación Media
2. Entidades de Dominio
2.1 Field (Campo)
El stakeholder establece que un Campo tiene únicamente un nombre. Las hectáreas no son una propiedad
del Campo sino del vínculo Lote-Campaña.
Problema:⚠ El campo HectareasTotales en la entidad Field es conceptualmente incorrecto según el
Core OT. Las hectáreas pertenecen a la relación CampaignLot, no al Campo.
Estado actual en repositorio Cambios necesarios
public class Field : TenantEntity
{
public string Name { get; set; }
public double HectareasTotales
{ get; set; } // ← INCORRECTO
public DateTime CreatedAt { get;
set; }
public ICollection<Lot> Lots { get;
set; }
public ICollection<WorkOrder>
WorkOrders { get; set; }
}
public class Field : TenantEntity
{
public string Name { get; set; }
// HectareasTotales eliminado: las
Ha viven en CampaignLot
public DateTime CreatedAt { get;
set; }
public ICollection<Lot> Lots { get;
set; }
public ICollection<WorkOrder>
WorkOrders { get; set; }
}
2.2 Lot (Lote)
La entidad Lot está bien concebida. La relación N:N con Campaign a través de CampaignLot es correcta.
Sin embargo, el polígono GIS (Geometry) está en el Lote como dato fijo, cuando debería existir también por
cada relación CampaignLot, ya que la superficie puede variar entre campañas.
Estado actual en repositorio Cambios necesarios
public class CampaignLot : TenantEntity
{
public Guid CampaignId { get; set; }
public Guid LotId { get; set; }
public decimal ProductiveArea { get;
set; }
public Guid? CropId { get; set; }
// Sin polígono propio
}
public class CampaignLot : TenantEntity
{
public Guid CampaignId { get; set; }
public Guid LotId { get; set; }
public decimal ProductiveArea { get;
set; }
public Guid? CropId { get; set; }
// Polígono propio del vínculo Lote-
Campaña.
// Al crear, se hereda del
Lot.Geometry por defecto.
public Geometry? Geometry { get;
set; }
}
Además, al crear un CampaignLot se debe copiar automáticamente el valor de Lot.CadastralArea como
valor por defecto de ProductiveArea, y Lot.Geometry como valor por defecto de CampaignLot.Geometry.
Esto ya ocurre parcialmente en CampaignManagerService para el área, y debe extenderse al polígono.
2.3 Campaign (Campaña)
La entidad Campaign ya cuenta con los campos requeridos (Name, StartDate, EndDate, Status con valor
Locked). La lógica de bloqueo existe a nivel de Campaña, pero presenta dos brechas:
• La cascada de bloqueo hacia WorkOrders y Labors no está implementada de forma completa.
Cuando una Campaña pasa a estado Locked, todas sus OT y Labores asociadas deben quedar en
modo solo lectura.
• No existe validación que impida asignar un Lote a dos Campañas cuyas fechas se superpongan.
Este es un requisito explícito del stakeholder.
Acción requerida:✎ Implementar la validación de solapamiento de fechas entre Campañas para un
mismo Lote, en el endpoint de asignación de Lotes a Campañas (CampaignsController). Retornar
HTTP 400 con mensaje descriptivo si hay superposición.
Ejemplo de lógica de validación a agregar en el endpoint de asignación de CampaignLot:
Estado actual en repositorio Cambios necesarios
// No existe validación de solapamiento
// El sistema permite asignar el mismo
Lote
// a dos Campañas con fechas
superpuestas.
// Antes de crear el CampaignLot,
verificar:
var overlap = await
_context.CampaignLots
.Include(cl => cl.Campaign)
.Where(cl => cl.LotId ==
request.LotId
&& cl.CampaignId !=
targetCampaignId
&& cl.Campaign.StartDate <
newCampaign.EndDate
&& cl.Campaign.EndDate >
newCampaign.StartDate)
.AnyAsync();
if (overlap)
return BadRequest("El Lote ya está
asignado a una
Campaña cuyas fechas se
superponen.");
2.4 Rotation (Rotación)
La entidad Rotation existe y contiene los campos básicos (CampaignLotId, StartDate, EndDate, CropName).
El servicio RotationService.cs está presente. Sin embargo, falta la validación de superposición de
Rotaciones dentro del mismo Lote, que es un requerimiento explícito y crítico del stakeholder.
Regla de negocio del stakeholder: No puede haber dos Actividades/Rotaciones simultáneas para el mismo
Lote en el mismo período. Las fechas pueden sobresalir del rango de la Campaña (con advertencia), pero
no pueden solaparse entre sí.
Estado actual en repositorio Cambios necesarios
// RotationService.cs actual:
// No valida solapamiento entre
rotaciones
// del mismo CampaignLot.
// Solo realiza consulta básica de
rotaciones
// activas para sugerir Actividad en
Labor.
// Agregar en RotationService o en el
Controller:
var overlap = await _context.Rotations
.Where(r => r.CampaignLotId ==
dto.CampaignLotId
&& r.Id != dto.Id // excluir sí
misma en edición
&& r.StartDate < dto.EndDate
&& r.EndDate > dto.StartDate)
.AnyAsync();
if (overlap)
return BadRequest("Ya existe una
Rotación en ese
período para el Lote/Campaña
indicado.");
// Advertencia si fechas salen del rango
de Campaña:
if (dto.StartDate < campaign.StartDate
||
dto.EndDate > campaign.EndDate)
warnings.Add("Las fechas de la
Rotación están fuera
del rango de la Campaña.");
2.5 Labor (Labor)
Esta es la entidad con más brechas respecto al Core OT. Se identifican tres problemas principales:
2.5.1 Diferenciación Planeada / Realizada
El stakeholder requiere que las Labores sean explícitamente Planeadas o Realizadas, y que coexistan
dentro de una OT pero que sean independientes. Las tablas de insumos de cada tipo se calculan por
separado. La implementación actual usa un campo Status (con valores "Planned" y "Realized") para
representar este concepto, lo que es funcionalmente equivalente pero genera ambigüedad.
El riesgo con el modelo actual es que una misma Labor puede cambiar de estado de Planned a Realized, lo
que implica que la Labor Planeada y la Realizada son el mismo registro. El stakeholder describe escenarios
donde la Labor Realizada puede entrar "por la ventana" sin que haya existido una Planeada, y donde los
datos pueden ser completamente distintos.
Acción requerida:✎ Se recomienda agregar un campo LaborMode (enumerado: Planned /
Realized) a la entidad Labor, independiente del Status. Esto permite que una Labor Realizada exista
sin un par Planeado, y que ambos coexistan en la misma OT sin ser el mismo registro.
Estado actual en repositorio Cambios necesarios
public class Labor : TenantEntity
{
// Status: "Planned", "Realized"
// Un mismo registro puede cambiar
de Planned a Realized.
// No es posible tener una Planeada
y una Realizada
// como entidades separadas e
independientes.
public string Status { get; set; } =
"Planned";
...
}
public class Labor : TenantEntity
{
// LaborMode identifica si la labor
es Planeada o Realizada.
// Status sigue siendo el estado de
proceso (Pending, Done...).
public LaborMode Mode { get; set; }
= LaborMode.Planned;
public string Status { get; set; } =
"Pending";
...
}
public enum LaborMode { Planned,
Realized }
2.5.2 Campos faltantes en Labor
El modal de creación de Labores actual carece de varios campos requeridos por el stakeholder. A
continuación se detalla cada uno:
• Persona responsable: El campo ContactId existe en la entidad pero no siempre está visible en el
modal. Debe estar siempre presente y mostrar si la persona es Propia o Contratista (con opción de
editar para esa instancia puntual).
• Tipo de Labor (LaborTaskType): Existe en la entidad y en el formulario. OK.
• Actividad (LaborType): Existe en la entidad. El formulario ya intenta sugerir la Actividad según la
Rotación activa. OK, pero requiere que las Rotaciones estén correctamente implementadas primero.
• Advertencia al superar hectáreas del CampaignLot: El campo Hectares existe pero no valida contra
el área productiva del CampaignLot. Si se ingresa un valor mayor, debe mostrar advertencia pero
permitir guardar.
• Asignación directa a una OT: El campo WorkOrderId existe en Labor. Falta la opción de asignar al
crear desde Labores Sueltas.
• Adjuntos (jpg, pdf, png, audio): No hay ninguna entidad ni campo que soporte archivos adjuntos en
Labor o en OT. Ver sección 2.6.
2.5.3 LaborSupply – Hectáreas por insumo
El stakeholder establece que cada insumo de una Labor puede aplicarse sobre una superficie diferente a la
de la Labor misma. El sistema debe precargar las Hectáreas de la Labor pero permitir editarlas por insumo.
Actualmente LaborSupply no tiene un campo Hectares propio.
Estado actual en repositorio Cambios necesarios
public class LaborSupply : ITenantEntity
{
public decimal PlannedDose { get;
set; } // dosis/ha
public decimal? RealDose { get; set;
}
public decimal PlannedTotal { get;
set; } // dose * labor.Ha
public decimal? RealTotal { get;
set; }
// Sin campo Hectares propio del
insumo.
}
public class LaborSupply : ITenantEntity
{
// Hectáreas sobre las que se aplica
ESTE insumo.
// Por defecto = Labor.Hectares,
editable.
public decimal PlannedHectares
{ get; set; }
public decimal? RealHectares { get;
set; }
public decimal PlannedDose { get;
set; } // coef/ha
public decimal? RealDose { get; set;
}
// PlannedTotal = PlannedHectares *
PlannedDose
public decimal PlannedTotal { get;
set; }
public decimal? RealTotal { get;
set; }
// Coef. y Total calculados (regla
de 3 sobre lo realizado):
public decimal? CalculatedDose
{ get; set; }
public decimal? CalculatedTotal
{ get; set; }
}
2.6 Adjuntos de Labor y OT
El stakeholder requiere poder adjuntar archivos (jpg, pdf, png, audio) a las Labores, y que una OT muestre
un listado consolidado de todos los adjuntos de sus Labores. Actualmente no existe ninguna entidad, tabla
ni campo para esto.
Acción requerida:✎ Crear la entidad LaborAttachment y su tabla correspondiente. Los archivos
deben almacenarse en el sistema de archivos o en un servicio de almacenamiento (Supabase
Storage ya está disponible en la infraestructura del proyecto).
Estado actual en repositorio Cambios necesarios
// No existe ninguna entidad para
adjuntos.
// Labor solo tiene:
// public string? PrescriptionMapUrl
{ get; set; }
// public string? EvidencePhotosJson
{ get; set; }
// (Enfoque de JSON, no tabla
relacional)
public class LaborAttachment :
TenantEntity
{
public Guid LaborId { get; set; }
public string FileName { get; set; }
public string FileUrl { get; set; }
public string MimeType { get; set; }
// image/jpeg, etc.
public long FileSizeBytes { get;
set; }
public DateTime UploadedAt { get;
set; }
public Labor? Labor { get; set; }
}
3. Orden de Trabajo (WorkOrder)
3.1 Estados de OT – Tabla configurable por el usuario
El stakeholder requiere que los estados de la OT sean configurables por el usuario. El sistema debe proveer
tres estados iniciales (por ejemplo: En proceso, Cerrada, Cancelada), pero el usuario debe poder crear,
editar y eliminar sus propios estados. Cada estado debe tener una bandera que indique si permite o no la
edición de la OT y sus Labores.
Brecha crítica:⚠ Los estados de OT están actualmente hardcodeados como strings ("Draft",
"InProgress", "Approved", "Cancelled"). No existe ninguna tabla WorkOrderStatus ni interfaz de
administración.
Estado actual en repositorio Cambios necesarios
// WorkOrder.Status es un string libre.
// No hay entidad WorkOrderStatus.
// Los estados posibles están dispersos
en el código:
// "Draft", "InProgress", "Approved",
// "Cancelled", "Completed",
"Pending",
// "Scheduled", "Done"
// Nueva entidad:
public class WorkOrderStatus :
TenantEntity
{
public string Name { get; set; }
// ej: "En Proceso"
public string ColorHex { get; set; }
// para la UI
public bool IsEditable { get; set; }
// permite editar OT
public bool IsDefault { get; set; }
// estado inicial
public int SortOrder { get; set; }
}
// WorkOrder usa FK en lugar de string:
public class WorkOrder : TenantEntity
{
public Guid WorkOrderStatusId { get;
set; }
public WorkOrderStatus?
WorkOrderStatus { get; set; }
// Mantener string Status para
compatibilidad
// durante la migración.
}
Adicionalmente, cuando una OT pase a un estado con IsEditable = false, deben bloquearse en cascada
todas sus Labores (Planeadas y Realizadas), quedando en modo solo consulta.
3.2 Modal de Creación de OT – Redundancias y campos faltantes
El stakeholder identifica que el modal de creación actual tiene una redundancia y varios campos faltantes:
• Selector de Campaña: Redundante. La campaña activa ya se selecciona en el menú lateral
(CampaignState). Debe eliminarse del modal y tomarse automáticamente del contexto de campaña.
• Costo estimado como campo manual: Incorrecto. Debe ser la sumatoria automática de los costos de
las Labores incluidas en la OT.
• Estado (WorkOrderStatusId): Debe mostrar los estados configurados por el usuario, no un string
libre.
• Persona responsable (ContactId) y bandera Propio/Contratista: Existen en la entidad pero deben
incluirse claramente en el modal.
• Reglas de herencia de Persona y Fecha a las Labores: El stakeholder plantea dos preguntas de
diseño que deben resolverse con el cliente antes de implementar: (a) ¿Las Labores heredan la
Persona del encabezado de OT, o pueden tener personas distintas? (b) ¿Todas las Labores deben
tener la misma fecha que el encabezado, o pueden tener fechas distintas? Estas decisiones afectan
directamente la validación del formulario de creación de Labor.
3.3 Vista de Detalle de OT – La brecha más crítica
La vista de detalle de la OT es la funcionalidad con mayor brecha respecto al Core OT. El stakeholder
describe una estructura de tres bloques bien definidos que no existe en la implementación actual.
Brecha crítica:⚠ La vista de detalle de OT no implementa la dualidad Planeado/Realizado ni las
tablas de insumos requeridas. Esta es la brecha más crítica del sistema.
La estructura requerida por el stakeholder para la vista de detalle de una OT es:
1. Sección Encabezado: datos editables de la OT (Estado, Nombre, Persona, tipo Propio/Contratista).
2. Tabla de Labores Asignadas: ID único, Nombre, Fecha de Ejecución, Tipo de Labor, Actividad,
Hectáreas, Lote/Campo, cantidad de Insumos, botón Ver Labor.
3. Pestaña PLANEADO – Tabla Total de Insumos: sumatoria por tipo de insumo de todas las Labores
Planeadas, con columna Retiro Aprobado (campo editable) y columna Centro (de dónde retirar).
4. Pestaña PLANEADO – Tabla Detalle de Insumos por Labor: Labor, Actividad, Lote, Campo, Insumo,
Ha, Coef/Ha, Cantidad, Unidad.
5. Pestaña PLANEADO – Lista de Adjuntos consolidada de todas las Labores.
6. Pestaña REALIZADO – Tabla Total de Insumos: igual que la de Planeado pero con columna Total
Utilizado (dato ingresado manualmente).
7. Pestaña REALIZADO – Tabla Detalle de Insumos por Labor: agrega columnas Coef. Calculado y
Cantidad Calculada (calculadas por regla de tres proporcional).
3.4 Retiro Aprobado y Centro de Retiro
En la tabla de Total de Insumos de la pestaña Planeado, el stakeholder requiere dos columnas que no
existen en ninguna entidad del sistema:
• Retiro Aprobado: cantidad real de insumo que se autoriza a retirar. Se ingresa manualmente (por
ejemplo, se redondea al envase más cercano). No es lo mismo que el Total calculado.
• Centro: punto de retiro del insumo (depósito, proveedor, etc.).
Acción requerida:✎ Crear la entidad WorkOrderSupplySummary (o agregar campos en un DTO de
resumen de OT) que almacene el Retiro Aprobado y el Centro por cada tipo de insumo por OT.
Estado actual en repositorio Cambios necesarios
// No existe entidad ni campo para esto.
// La tabla de insumos totales no está
implementada.
public class WorkOrderSupplyApproval :
TenantEntity
{
public Guid WorkOrderId { get;
set; }
public Guid SupplyId { get; set; }
public decimal TotalCalculated
{ get; set; } // sumatoria
public decimal ApprovedWithdrawal
{ get; set; } // editable
public string? WithdrawalCenter
{ get; set; } // editable
public WorkOrder? WorkOrder { get;
set; }
public Inventory? Supply { get; set;
}
}
3.5 Coeficiente y Total Calculado (Sección Realizado)
En la pestaña Realizado de la OT, la tabla de Detalle de Insumos por Labor debe mostrar dos columnas
calculadas: Coef. Calculado y Cantidad Calculada. Estos valores se obtienen por regla de tres proporcional
sobre lo planeado.
Lógica de cálculo (según el ejemplo del stakeholder): Si en el planeamiento un insumo X se usa en dos
labores con coef. 2, la Labor A tiene 10 Ha (total = 20) y la Labor B tiene 5 Ha (total = 10). Total planeado =
30. Si el total real gastado fue 29, la proporción de la Labor A es 20/30 = 66,7%, luego su Cantidad
Calculada = 29 × 66,7% = 19,33. El Coef. Calculado = 19,33 / Ha_realizadas_Labor_A.
Estos valores deben calcularse en el backend al construir el DTO de detalle de la OT, no almacenarse en la
base de datos (son datos derivados). Sin embargo, los campos TotalUtilizado por insumo (que el usuario
ingresa manualmente en la sección Realizado) sí deben persistirse.
Acción requerida:✎ Agregar el campo RealTotalUsed (editable por el usuario) en
WorkOrderSupplyApproval o en un campo equivalente, y calcular los valores Coef. Calculado y
Cantidad Calculada al construir el DTO de respuesta.
4. Bloqueo en Cascada – Campaña → OT → Labor
El stakeholder requiere que al bloquear una Campaña, se bloqueen en cascada todas las OT y Labores
asociadas. La implementación actual tiene el CampaignLockedInterceptor que previene modificaciones de
CampaignLots en Campañas bloqueadas, pero no extiende este bloqueo a WorkOrders y Labors.
De manera similar, al bloquear una OT (poniéndola en un estado con IsEditable = false), deben bloquearse
todas sus Labores.
Estado actual en repositorio Cambios necesarios
// CampaignLockedInterceptor.cs bloquea
solo
// modificaciones en CampaignLots.
// No verifica WorkOrders ni Labors.
// WorkOrdersController.cs: al aprobar
una OT,
// solo verifica que todas las Labores
sean Realized.
// No bloquea edición posterior de las
Labores.
// Extender CampaignLockedInterceptor
para incluir
// WorkOrders y Labors bajo campañas
bloqueadas.
// Agregar en WorkOrdersController:
// Al cambiar el status de una OT a un
estado no editable,
// actualizar en cascada todas las
Labors asociadas.
// Opción alternativa: mover la lógica
de bloqueo
// a un servicio de dominio
WorkOrderBlockingService
// que sea invocado tanto desde el
interceptor como
// desde el controller de cambio de
estado.
5. Interfaz de Usuario – Brechas en Blazor
5.1 Selector de Campaña en modal de OT
El modal de creación de OT (OrdenesTrabajos.razor) incluye actualmente un selector de Campaña. Esto es
redundante porque la campaña activa ya existe en el CampaignState inyectado en el componente.
Acción requerida:✎ Eliminar el selector de Campaña del modal de creación de OT y usar
directamente CampaignState.CurrentCampaign.Id al crear la OT.
5.2 LaborEditorForm – Advertencia de Hectáreas
El formulario LaborEditorForm.razor muestra el campo Hectáreas pero no compara el valor ingresado contra
CampaignLot.ProductiveArea. Debe mostrar una advertencia (sin bloquear) si el usuario ingresa más
hectáreas de las definidas para ese CampaignLot.
Estado actual en repositorio Cambios necesarios
// LaborEditorForm.razor:
<FormItem Label="Hectáreas">
<InputNumber @bind-
Value="_model.Hectares"
Min="0m" Step="0.1m" />
</FormItem>
// Sin validación contra
CampaignLot.ProductiveArea
// Agregar lógica de advertencia:
private bool _haSuperaLote;
private void OnHectareasChanged(decimal
val) {
_model.Hectares = val;
var lote = _lots.FirstOrDefault(
l => l.LotId == _model.LotId);
_haSuperaLote = lote != null
&& val > lote.ProductiveArea;
}
// En el template:
@if (_haSuperaLote) {
<Alert Type="warning"
Message="Superás las Ha del Lote" />
}
5.3 Página de Detalle de OT – Estructura completa
La página actual (OrdenesTrabajos.razor) muestra la lista de OTs y permite ver el detalle básico, pero no
implementa la estructura de dos pestañas (Planeado / Realizado) con las tablas de insumos. Esta es la vista
más importante del sistema según el stakeholder.
Se recomienda crear una página dedicada WorkOrderDetail.razor (o un componente de alto nivel) con la
siguiente estructura de alto nivel:
• Header editable: Estado (configurable), Nombre, Persona, Propio/Contratista.
• Tabla de Labores Asignadas: listado con columnas requeridas y botón Ver Labor.
• Tabs Planeado / Realizado:
◦ Tab Planeado: tabla Total Insumos (con Retiro Aprobado y Centro editables) + tabla Detalle por
Labor + adjuntos.
◦ Tab Realizado: tabla Total Insumos (con Total Utilizado editable) + tabla Detalle por Labor (con
Coef. y Total Calculados).
Infraestructura disponible:✔ Esta vista debe poder compartirse con el contratista en modo solo
lectura (tab Planeado). El mecanismo de ShareController y tokens compartidos ya existe en el
repositorio y puede aprovecharse.
6. Hoja de Ruta – Orden Sugerido de Implementación
Las brechas identificadas se ordenan a continuación por dependencia técnica y prioridad de negocio:
8. Agregar LaborMode (Planned/Realized) a la entidad Labor y migrar los valores existentes. [Dominio]
9. Agregar PlannedHectares / RealHectares a LaborSupply, y CalculatedDose / CalculatedTotal.
[Dominio]
10. Crear entidad WorkOrderStatus con campo IsEditable. Migrar strings de estado a FK. [Dominio +
Infraestructura]
11. Crear entidad LaborAttachment. [Dominio + Infraestructura]
12. Crear entidad WorkOrderSupplyApproval (Retiro Aprobado + Centro + Total Utilizado). [Dominio +
Infraestructura]
13. Agregar Geometry a CampaignLot. Actualizar CampaignManagerService para copiar polígono del
Lote por defecto. [Dominio + Infraestructura]
14. Eliminar HectareasTotales de Field. [Dominio]
15. Implementar validación de solapamiento de Campañas por Lote en CampaignsController. [API]
16. Implementar validación de solapamiento de Rotaciones en RotationService. [Infraestructura]
17. Extender CampaignLockedInterceptor para bloquear WorkOrders y Labors en cascada.
[Infraestructura]
18. Implementar lógica de bloqueo de Labors al cambiar estado de OT a IsEditable=false. [API]
19. Implementar cálculo de Coef. y Total Calculado al construir WorkOrderDetailDto. [Infraestructura /
Query]
20. Actualizar LaborEditorForm.razor: advertencia de Ha, modal de OT sin selector de Campaña.
[Frontend]
21. Crear interfaz de administración de WorkOrderStatus (CRUD). [Frontend]
22. Construir página WorkOrderDetail.razor con estructura completa Planeado/Realizado. [Frontend]
7. Consideraciones Adicionales
7.1 Pregunta de diseño pendiente – Reglas de herencia en OT
El stakeholder plantea explícitamente dos preguntas de diseño sin respuesta definida que deben
consensuarse antes de implementar:
• ¿Las Labores dentro de una OT heredan la Persona asignada en el encabezado, o pueden tener
personas diferentes?
• ¿Todas las Labores de una OT deben tener la misma fecha que el encabezado, o pueden tener
fechas distintas?
La respuesta a estas preguntas determina si el modal de Labor necesita mostrar u ocultar campos de
Persona y Fecha cuando se crea una Labor dentro de una OT.
7.2 Concatenación y División de Lotes
El stakeholder menciona como trabajo futuro la necesidad de manejar el historial de insumos cuando un
Lote se divide o cuando varios Lotes se fusionan para formar uno nuevo. Esta funcionalidad no está dentro
del alcance del Core OT actual, pero debe tenerse en cuenta en el diseño del modelo de datos para no
cerrarse puertas.
7.3 Lo que ya funciona correctamente
Elementos OK:✔ Los siguientes elementos del repositorio están implementados de forma correcta y
alineada con el Core OT: arquitectura multi-tenant, contexto de campaña activa en la UI, relación
CampaignLot N:N, sugerencia de Actividad por Rotación activa en LaborEditorForm, mecanismo de
share público de OT, integración con ERP Gestor Max, validación de mezcla de tanque
(TankMixRules), y exportación ISO-XML.
GestorOT · Análisis de Brechas · Abril 2026