GestorOT
Plan de Implementación Técnico
Fix Plan v1.0  ·  Basado en ot_viewmodal.md

Stack	.NET 9 Web API · Blazor WASM · Entity Framework Core · PostgreSQL + PostGIS · Ant Design Blazor
Proyecto analizado	Gestor-OT-main (repositorio completo inspeccionado)
Bloques cubiertos	5 (Validaciones · GIS · OT & Labores · Exportación · UI/Dark Mode)

Resumen de Arquitectura del Repositorio
Tras inspeccionar el repositorio, la arquitectura sigue un patrón de Clean Architecture con cuatro proyectos principales:

Capa	Proyecto / Responsabilidad
Domain	GestorOT.Domain — Entidades (Labor, Lot, Rotation, WorkOrder, CampaignLot…), Enums, Domain Events
Application	GestorOT.Application — Interfaces de repositorio, servicios de aplicación (IAgronomicValidationService, IRotationService, IWorkOrderQueryService, etc.), DTOs de aplicación
Infrastructure	GestorOT.Infrastructure — EF Core (ApplicationDbContext, Configurations, Migrations), servicios concretos (AgronomicValidationService, RotationService, LotQueryService, IsoXmlExporterService, etc.), Interceptors (Audit, CampaignLocked, TenantSession)
API + Client	GestorOT.Api — Controllers REST (.NET); GestorOT.Client — Blazor WASM (Pages, Components, Services de estado); GestorOT.Shared — DTOs compartidos, Validation/ApiRequestDtos

Puntos clave detectados:
•	El tenant se inyecta en todas las queries via TenantSessionInterceptor y CampaignHttpHandler.
•	La entidad Labor ya tiene Mode (Planned/Realized) y Status como campos separados — base ideal para clonar planeada/realizada.
•	CampaignLot vincula Campaign ↔ Lot y contiene ProductiveArea; Rotation vincula CampaignLot con la actividad del ERP y el rango de fechas.
•	Lot.CadastralArea existe en el dominio pero NO se calcula ni persiste automáticamente al guardar geometría GIS — es el bug del total catastral en 0.
•	WorkOrderQueryService tiene un bug en la proyección de Supplies: al mapear los labors, solo captura Supplies del último labor si no se itera correctamente (confirmado en el código fuente).
•	El CSS usa variables de color correctamente para el modo oscuro, pero hay textos con color: black inyectados por Ant Design que escapan a los overrides en main-layout.css.

BLOQUE 1  Validaciones y Lógica de Negocio (Labores)

1.1 · Validación de Superficie (Hectáreas ≤ Área Productiva del Lote)
Estado actual: El formulario LaborEditorForm.razor ya muestra un warning visual (_haSuperaLote) cuando se supera el área productiva, pero el backend no valida — se puede guardar igual.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LaborsController.cs	Agregar validación hard-stop en POST y PUT
AgronomicValidationService.cs	Extraer lógica al servicio dedicado (nuevo método)
IAgronomicValidationService.cs	Declarar ValidateLaborSurfaceAsync(...)
LaborEditorForm.razor	Bloquear submit si _haSuperaLote == true

Plan de Acción Frontend
•	En LaborEditorForm.razor, localizar el botón de submit (HandleSubmit). Agregar guarda condicional:
○	if (_haSuperaLote) { Message.Error("La superficie supera el área productiva del lote."); return; }
•	Cambiar el color del Warning de #F1C40F a rojo (#E74C3C) para mayor severidad visual.
•	Bloquear el botón con Disabled="@_haSuperaLote" para feedback inmediato.

Plan de Acción Backend & DB
•	En IAgronomicValidationService.cs, agregar: Task<bool> ValidateLaborSurfaceAsync(Guid campaignLotId, decimal hectares)
•	En AgronomicValidationService.cs, implementar: consultar CampaignLot.ProductiveArea y retornar false si hectares > productiveArea.
•	En LaborsController.cs, inyectar IAgronomicValidationService. En los endpoints POST y PUT de /api/labors, invocar el método antes de SaveChangesAsync; devolver 400 Bad Request con mensaje claro si falla.
•	No se requiere migración de base de datos — ProductiveArea ya existe en CampaignLot.

1.2 · Autocompletado de Actividad desde Rotación Activa
Estado actual: El frontend ya llama a CheckRotation() y bloquea el selector de actividad (_actividadLocked = true). El backend en RotationsController expone GET .../rotations/active?date=. El autocompletado funciona en UI, pero el backend no revalida la coherencia al guardar.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LaborsController.cs	Agregar validación de actividad vs rotación en POST/PUT
IAgronomicValidationService.cs	Declarar ValidateLaborActivityMatchesRotationAsync(...)
AgronomicValidationService.cs	Implementar validación de actividad

Plan de Acción Frontend
El autocompletado ya está implementado correctamente. Refuerzo recomendado:
•	Si la actividad está bloqueada (_actividadLocked) y el usuario intenta desbloquearlo manualmente, mostrar un Popconfirm de confirmación antes de sobreescribir la sugerencia de rotación.

Plan de Acción Backend & DB
•	En AgronomicValidationService.cs, nuevo método: dado CampaignLotId, fecha y ErpActivityId, consultar la rotación activa. Si existe rotación y la actividad no coincide, retornar warning (no error hard — puede ser labor de otro cultivo).
•	En LaborsController.cs (POST/PUT), invocar la validación; si hay desajuste, incluir un campo warnings en la respuesta 200 OK (no 400) para que el cliente pueda mostrar un Tooltip de alerta.

1.3 · Validación de Fechas dentro del Rango de Rotación
Estado actual: El frontend valida visualmente via CheckRotation() pero no impide guardar fuera del rango.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LaborEditorForm.razor	Agregar validación de fechas contra rango de rotación activa
LaborsController.cs	Validación backend de fechas vs rotación
IAgronomicValidationService.cs	Declarar ValidateLaborDatesInRotationAsync(...)
AgronomicValidationService.cs	Implementar validación de fechas

Plan de Acción Frontend
•	En LaborEditorForm.razor, cuando _activeRotation != null, al cambiar EstimatedDate o ExecutionDate, verificar si la fecha cae fuera de [_activeRotation.StartDate, _activeRotation.EndDate].
•	Si está fuera, mostrar Alert de Warning bajo el DatePicker con el texto: "Fecha fuera del rango de rotación ({StartDate} - {EndDate})"
•	Agregar un flag _fechaFueraRotacion y incluirlo en la guarda del submit.

Plan de Acción Backend & DB
•	En AgronomicValidationService.cs: método ValidateLaborDatesInRotationAsync(Guid campaignLotId, DateTime? estimatedDate, DateTime? executionDate). Recuperar rotación activa para la fecha; verificar que ambas fechas (si existen) estén dentro del rango.
•	En LaborsController.cs (POST/PUT): invocar antes de SaveChanges. Retornar 400 con mensaje descriptivo si falla.

BLOQUE 2  Módulo GIS y Gestión de Lotes

2.1 · Buscador de Ciudad en la Pantalla GIS
Estado actual: Mapa.razor no tiene input de búsqueda. El mapa usa Leaflet via mapInterop.js.

Archivos a Modificar / Crear
Archivo / Componente	Acción
Mapa.razor	Agregar componente de búsqueda en top-center
mapInterop.js	Agregar función searchCity(query) usando geocoder
GestorOT.Api/wwwroot/css/main-layout.css	Estilos para el buscador GIS

Plan de Acción Frontend
•	En Mapa.razor, agregar un <div class="gis-search"> posicionado top: 12px; left: 50%; transform: translateX(-50%); z-index: 1000 que contenga un <Input> de AntDesign con placeholder="Buscar ciudad..." y un ícono de lupa.
•	Al presionar Enter o el botón de búsqueda, invocar JS.InvokeVoidAsync("mapInterop.searchCity", _cityQuery).
•	En mapInterop.js, implementar searchCity(query) usando la API de Nominatim (OpenStreetMap) vía fetch: GET https://nominatim.openstreetmap.org/search?q={query}&format=json&limit=1. Con el resultado, llamar map.flyTo([lat, lng], 12).

Plan de Acción Backend & DB
No se requieren cambios en el backend. La búsqueda es 100% client-side vía Nominatim (API pública, sin API key).
Nota: Si el ambiente es privado/offline, considerar Photon geocoder (self-hosteable) como alternativa a Nominatim.

2.2 · Crear Lote desde GIS cuando no hay huérfanos disponibles
Estado actual: Al dibujar un polígono en Mapa.razor, si _orphanLots está vacío, el dropdown de vinculación queda vacío y el usuario no puede guardar.

Archivos a Modificar / Crear
Archivo / Componente	Acción
Mapa.razor	Agregar flujo de creación de lote inline
LotsController.cs	POST /api/lots ya existe — solo verificar que devuelva el ID
FieldsController.cs	GET /api/fields — para el selector de campo al crear lote

Plan de Acción Frontend
•	En el rail lateral de Mapa.razor (sección "Polígono Dibujado"), detectar si _orphanLots.Count == 0. Si es así, mostrar un Button secundario "Crear nuevo lote" bajo el Select de vinculación.
•	Al hacer clic, abrir un sub-formulario inline (dentro del rail) con dos campos: Select de Campo (Field) y Input de Nombre del lote.
•	Al confirmar, llamar POST /api/lots con { FieldId, Name, Status: "Active" }. El ID del nuevo lote se usa para _linkingLotId y se llama SaveDrawnGeometry().
•	Tras guardar, agregar el lote recién creado a _allLots y limpiar el sub-formulario.

Plan de Acción Backend & DB
•	POST /api/lots ya existe en LotsController.cs y crea el lote correctamente. No se requieren cambios.
•	Verificar que la respuesta incluya el ID generado (CreatedAtAction con el objeto) — ya está implementado.

2.3 · Calcular y Persistir CadastralArea al guardar geometría
Estado actual (Bug): Lot.CadastralArea existe en la entidad pero jamás se calcula automáticamente. En LotsController.cs (PUT /api/lots/{id}), se guarda la geometría pero no se recalcula el área catastral.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LotsController.cs	Modificar PUT para calcular y persistir CadastralArea
LotQueryService.cs	Exponer método GetLotAreaByIdAsync(Guid) para reutilizar lógica SQL
ILotQueryService.cs	Agregar declaración del método

Plan de Acción Backend & DB
•	En LotsController.cs, en el endpoint PUT /api/lots/{id}, después de asignar lot.Geometry y antes de SaveChangesAsync(), agregar el cálculo de área vía SQL espacial:
○	var area = await _context.Database.SqlQueryRaw<double>("SELECT ST_Area($1::geography)/10000.0 AS \"Value\"", wkt).FirstOrDefaultAsync();
○	Alternativamente, usar NetTopologySuite: lot.CadastralArea = (decimal)(lot.Geometry.Area * 10000); pero con proyección geográfica correcta (EPSG:4326 requiere conversión).
•	RECOMENDADO: Usar el mismo patrón SQL que ya existe en LotQueryService.GetLotAreasAsync() — ejecutar la query ST_Area(...::geography)/10000 para el lote recién guardado y asignar el resultado a lot.CadastralArea antes del commit.
•	En POST /api/lots (creación): aplicar la misma lógica si se recibe WktGeometry en la request.
No se requiere migración: el campo CadastralArea ya existe en la entidad Lot y en la tabla correspondiente (confirmado en el snapshot de EF Core).

2.4 · Corrección Bug: Total Catastral en 0 en Pantalla de Lotes por Campaña
Estado actual: CampaignLotEditor.razor o Lotes.razor muestran total catastral = 0 porque CadastralArea nunca fue persistido (fix 2.3 lo resuelve) y además el DTO de CampaignLot no expone CadastralArea.

Archivos a Modificar / Crear
Archivo / Componente	Acción
CampaignDto.cs (Shared)	Agregar CadastralArea en CampaignLotDto
CampaignsController.cs	Incluir lot.CadastralArea en la proyección de GET .../lots
CampaignLotEditor.razor	Mostrar el dato en la UI
Lotes.razor	Mostrar total catastral y variación de superficie

Plan de Acción Frontend
•	En CampaignDto.cs (Shared), agregar la propiedad CadastralArea en el record CampaignLotDto.
•	En Lotes.razor o CampaignLotEditor.razor, agregar columna "Área Catastral" y una columna calculada "Variación" = ProductiveArea - CadastralArea, con color rojo si negativa y verde si positiva.

Plan de Acción Backend & DB
•	En CampaignsController.cs, en el endpoint GET /api/campaigns/{id}/lots, incluir en la proyección: CadastralArea = cl.Lot != null ? cl.Lot.CadastralArea : 0m.
•	Con el fix 2.3, los lotes tendrán CadastralArea correctamente poblado. Ejecutar un UPDATE masivo post-deploy para recalcular el área de lotes históricos con geometría.

2.5 · Historial de Variación de Superficie Productiva por Campaña
Archivos a Modificar / Crear
Archivo / Componente	Acción
CampaignsController.cs	Nuevo endpoint GET /api/lots/{lotId}/surface-history
ILotQueryService.cs	Declarar GetSurfaceHistoryAsync(Guid lotId)
LotQueryService.cs	Implementar consulta de historial cross-campaign
Lotes.razor / CampaignLotEditor.razor	Componente de historial (Tabs o Collapse)

Plan de Acción Backend & DB
•	Crear endpoint GET /api/lots/{lotId}/surface-history que consulte todos los CampaignLot del lote ordenados por Campaign.StartDate, retornando { CampaignName, StartDate, ProductiveArea, CadastralArea, Variation }.
•	No requiere migración; los datos ya existen en CampaignLot.

Plan de Acción Frontend
•	En la pantalla de detalle de lote, agregar un componente Collapse o un Chart de barras (Ant Design Charts o una tabla simple) que muestre el historial de superficie productiva vs catastral por campaña.

BLOQUE 3  Órdenes de Trabajo (OT) y Labores

3.1 · Selector de Campaña al crear OT
Estado actual: OrdenesTrabajos.razor y el formulario de creación de OT no tienen selector de campaña. WorkOrder.CampaignId existe en el dominio y en WorkOrderDto pero no se expone en el formulario de creación.

Archivos a Modificar / Crear
Archivo / Componente	Acción
OrdenesTrabajos.razor	Agregar Select de campañas con preselección activa
WorkOrderDto.cs (Shared)	Ya tiene CampaignId — sin cambios
WorkOrdersController.cs	Verificar que POST /api/workorders persiste CampaignId

Plan de Acción Frontend
•	En el modal/formulario de creación de OT dentro de OrdenesTrabajos.razor, agregar un <Select> de campañas cargado desde GET /api/campaigns.
•	Al abrir el formulario, preseleccionar CampaignState.CurrentCampaign.Id. El usuario puede cambiarlo manualmente.
•	Incluir el CampaignId en el body del POST.

Plan de Acción Backend & DB
•	En WorkOrdersController.cs, verificar que el endpoint POST /api/workorders ya persiste CampaignId (confirmado en el código — sí lo hace).
•	Sin cambios de migración requeridos.

3.2 · Ejecución de Labores Sueltas con Trazabilidad (Planeada ↔ Realizada)
Estado actual: LaboresSueltas.razor permite eliminar y asignar labores a OT, pero no tiene un botón "Ejecutar" inline. La entidad Labor ya tiene Mode (LaborMode enum: Planned/Realized) y el endpoint POST /api/labors/{id}/replicate ya clona el registro. La lógica necesita ser orquestada correctamente.

Punto de mayor complejidad: requiere coordinar la clonación de la labor, la actualización de estado y la asignación a OT en una transacción atómica.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LaboresSueltas.razor	Agregar botón "Ejecutar" por labor + modal de confirmación con insumos
LaborsController.cs	Nuevo endpoint POST /api/labors/{id}/execute-standalone
WorkOrderDetail.razor	Mostrar ambas versiones (Planned/Realized) con tabs
WorkOrderQueryService.cs	Incluir ambas versiones en la query de detalle
LaborDto.cs (Shared)	Ya tiene Mode — verificar que se mapea en las queries

Plan de Acción Frontend — LaboresSueltas.razor
•	Agregar un botón <Button Icon="play-circle"> "Ejecutar" a la derecha de cada fila de labor.
•	Al hacer clic, abrir un Modal con el listado de insumos planeados pre-cargados. El usuario puede modificar las dosis reales antes de confirmar.
•	Al confirmar, llamar POST /api/labors/{id}/execute-standalone con los insumos reales.
•	Tras la respuesta exitosa, la labor original desaparece de la lista de "sueltas" (ya que queda en estado Realized) y aparece la nueva realizada con opción "Asignar a OT".

Plan de Acción Backend & DB — Nuevo endpoint execute-standalone
•	Crear POST /api/labors/{id}/execute-standalone en LaborsController.cs que ejecute en una transacción:
○	1. Cargar la labor original (Planned). Verificar que Mode == Planned y Status != Realized.
○	2. Clonar la labor: crear un nuevo registro Labor con Mode = LaborMode.Realized, Status = "Realized", ExecutionDate = DateTime.UtcNow, copiando todos los demás campos.
○	3. Clonar los LaborSupply: para cada supply original, crear una copia en el nuevo labor con RealDose y RealTotal calculados según los valores enviados en el body.
○	4. Mantener la labor original intacta (Mode = Planned permanece). NO modificar la labor original.
○	5. La nueva labor realizada queda con WorkOrderId = null (suelta) hasta que el usuario la asigne.
○	6. Commit de transacción. Retornar el ID del nuevo labor realizado.

Plan de Acción Frontend — WorkOrderDetail.razor (si se asigna a OT)
•	En WorkOrderDetail.razor, en la TabPane "Labores y Tareas" (Key 1), agrupar las labores por LotId + LaborTypeId.
•	Para grupos donde existan pares Planned + Realized, renderizar sub-tabs internos: "Planeado" y "Realizado".
•	La columna Desviación muestra la diferencia de hectáreas y dosis entre ambas versiones.

Plan de Acción Backend & DB — WorkOrderQueryService
•	En WorkOrderQueryService.GetByIdAsync(), la query ya incluye Labors. Verificar que el mapeo proyecta el campo Mode correctamente (ya está en el LaborDto).
•	Agregar campo PlannedLaborId nullable a Labor (FK self-referential) para vincular explícitamente la versión realizada con su original. Esto permite agrupar en la UI sin heurísticas.
○	Migración: ALTER TABLE "Labors" ADD COLUMN "PlannedLaborId" uuid NULL REFERENCES "Labors"("Id");
○	En el endpoint execute-standalone, asignar newLabor.PlannedLaborId = originalLaborId.

3.3 · Bug: Insumos Planeados en Detalle de OT — Solo muestra el último labor
Estado actual: En WorkOrderDetail.razor TabPane "Planeado (Insumos)", cuando la OT tiene más de 2 labores con insumos, solo se renderiza el insumo del último labor. El bug está en WorkOrderQueryService.cs en la proyección de SupplyApprovals, que parece consolidar incorrectamente.

Archivos a Modificar / Crear
Archivo / Componente	Acción
WorkOrderQueryService.cs	Revisar la proyección de SupplyApprovals en GetByIdAsync()
WorkOrdersController.cs	Endpoint de consolidación ConsolidateSupplies — revisar lógica
WorkOrderDetail.razor	Verificar binding de DataSource en la tabla de insumos

Diagnóstico y Plan de Acción Backend
•	En WorkOrderQueryService.cs, inspeccionar el mapeo de SupplyApprovals. El bug probable: el Select proyecta solo el primer o último elemento de la colección anidada.
•	FIX: Asegurarse de que el .Include(w => w.SupplyApprovals).ThenInclude(a => a.Supply) y la proyección final iteran TODOS los registros de SupplyApprovals:
SupplyApprovals = workOrder.SupplyApprovals.Select(a => new WorkOrderSupplyApprovalDto(...)).ToList()
•	Revisar también el endpoint ConsolidateSupplies (botón "Sincronizar de Labores") en WorkOrdersController.cs: la lógica debe agrupar por SupplyId sumando PlannedTotal de TODOS los labors, no sobreescribiendo.

Plan de Acción Frontend
•	En WorkOrderDetail.razor, la Table de insumos tiene DataSource="_order.SupplyApprovals" — verificar que _order.SupplyApprovals se bindea correctamente luego del fix backend.
•	Si el bug también existe al renderizar la lista de Labors con Supplies en el Tab 1, verificar que la columna de insumos itera supply.Supplies sin cortar la colección.

BLOQUE 4  Exportación e Integraciones

4.1 · Exportación Interactiva de Labores a HTML con Feedback de Insumos Reales
Estado actual: El sistema tiene IsoXmlExporterService.cs que genera archivos ISO XML/ZIP. No existe exportador HTML interactivo. El flujo requiere: generar HTML → usuario completa insumos en campo → HTML envía datos a API → API ejecuta la labor en background.

Este es el requerimiento de mayor superficie técnica. Requiere: generador HTML server-side, endpoint de recepción de datos del HTML, procesamiento background y cálculo de desviaciones.

Archivos a Modificar / Crear
Archivo / Componente	Acción
IHtmlLaborExporterService.cs (nuevo)	Interfaz en GestorOT.Application.Services
HtmlLaborExporterService.cs (nuevo)	Implementación en GestorOT.Infrastructure.Services
WorkOrdersController.cs	GET /api/workorders/{id}/export-html (descarga)
PublicWorkOrder.razor (existente)	Reutilizar o extender para recepción de datos del HTML
ShareController.cs (existente)	Agregar endpoint POST /api/share/realize-from-html
SharedToken.cs (existente)	Verificar que soporta tokens para el flujo público
LaborExecutionBackgroundService.cs (nuevo)	IHostedService para procesamiento async (opcional)

Flujo Técnico Detallado
•	PASO 1 — Generar el HTML: El endpoint GET /api/workorders/{id}/export-html genera un archivo HTML self-contained que incluye:
○	Lista de labores planeadas con sus insumos (dosis planificadas pre-rellenas).
○	Formulario editable por cada insumo (campos: dosis real, hectáreas reales).
○	Un token de un solo uso (SharedToken con ExpiresAt = DateTime.UtcNow.AddDays(30)) embebido en el HTML.
○	El HTML tiene un botón "Enviar datos" que hace un fetch POST a la URL pública de la API con el token y los datos completados.
•	PASO 2 — Recepción de datos: El endpoint POST /api/share/realize-from-html recibe el token, los datos de insumos reales y ejecuta la labor (lógica del punto 3.2) de forma síncrona o encola en background.
•	PASO 3 — Procesamiento: Crear los registros Realized (clonar labor, aplicar dosis reales, calcular desviaciones) y marcar el token como usado (IsUsed = true).
•	PASO 4 — Confirmación: La respuesta del POST devuelve un JSON con el resumen de la ejecución. El HTML muestra un mensaje de confirmación al usuario de campo.

Plan de Acción Frontend
•	En WorkOrderDetail.razor, agregar un botón "Exportar HTML Interactivo" junto a los botones existentes.
•	Al hacer clic, invocar GET /api/workorders/{id}/export-html y triggerear una descarga del archivo .html vía JS (crear un Blob URL y simular un click en <a download>).
•	PublicWorkOrder.razor ya maneja el flujo de OT pública — se puede extender para mostrar el formulario de insumos reales en vez del detalle read-only.

Plan de Acción Backend & DB
•	En HtmlLaborExporterService.cs, el HTML generado debe ser completamente autónomo (CSS inline, JS inline) para funcionar sin conexión a internet desde el campo.
•	El fetch del HTML debe apuntar a la URL de producción de la API — incluir como variable en el HtmlLaborExporterService configurada vía IConfiguration.
•	El SharedToken ya tiene los campos necesarios (Token, ExpiresAt, WorkOrderId). Agregar campo IsUsed (bool) si no existe para invalidar el token post-submit.
○	Migración: ALTER TABLE "SharedTokens" ADD COLUMN IF NOT EXISTS "IsUsed" boolean NOT NULL DEFAULT false;

BLOQUE 5  Interfaz de Usuario — Dark Mode & Contraste

5.1 · Eliminar tipografías en color negro — Dark Mode
Estado actual: main-layout.css ya tiene overrides globales para Ant Design (tablas, modales, forms, etc.) usando color: rgba(255,255,255,0.85). Sin embargo, algunos componentes de Ant Design inyectan estilos inline o clases específicas que no son cubiertas por los selectores actuales.

Archivos a Modificar / Crear
Archivo / Componente	Acción
main-layout.css	Agregar selectores faltantes para componentes problemáticos
admin.css	Revisar overrides en sección Admin
Componentes Razor individuales	Eliminar style="color: black" hardcodeados si existen

Plan de Acción Frontend
•	Identificar los selectores faltantes realizando una búsqueda en el DOM del browser en producción. Los más frecuentes en Ant Design que escapan a overrides globales:
○	.ant-typography, .ant-typography p, h1.ant-typography → agregar color: rgba(255,255,255,0.85)
○	.ant-descriptions-item-label, .ant-descriptions-item-content → color: rgba(255,255,255,0.7)
○	.ant-tabs-tab → color: rgba(255,255,255,0.6); .ant-tabs-tab-active → color: #E74C3C
○	.ant-steps-item-title, .ant-steps-item-description → color: rgba(255,255,255,0.7)
○	.ant-collapse-header → color: rgba(255,255,255,0.85)
○	.ant-list-item-meta-title, .ant-list-item-meta-description → color apropiado
○	.ant-tag → verificar que no tengan color de fondo que fuerce texto negro
•	Agregar regla catch-all al final del CSS (con baja especificidad): * { color: inherit; } y en :root definir --ant-color-text: rgba(255,255,255,0.85).
•	Para los Tags de AntDesign en WorkOrderDetail.razor y LaboresSueltas.razor: revisar que ningún Tag tenga Color="default" (que en Ant Design v5 es negro sobre gris claro). Usar colores explícitos siempre.
•	Buscar en todos los archivos .razor los patrones style="color: #000", style="color: black", style="color:#333" y reemplazar por color apropiado.

Plan de Acción Backend & DB
No aplica — es únicamente un fix de presentación en el cliente.

BLOQUE ★  Consideraciones Especiales y Riesgos

Riesgos y Mitigaciones por Bloque
Archivo / Componente	Acción
Bloque / Riesgo	Mitigación Recomendada
3.2 — Concurrencia en clonación de Labor	Usar IDbContextFactory y transacción explícita (BeginTransactionAsync) para garantizar atomicidad. Agregar índice único en (PlannedLaborId, Status) para evitar doble ejecución.
4.1 — Procesamiento HTML en background	Si el procesamiento falla a mitad, el token queda "used" pero la labor no se clonó. Usar el patrón Outbox o marcar el token como "processing" antes de empezar y "used" solo al finalizar con éxito.
4.1 — Seguridad del token en HTML	El token debe ser un GUID criptográficamente aleatorio (Guid.NewGuid() no es suficiente — usar RandomNumberGenerator.GetBytes()). Incluir fecha de expiración corta (7-15 días).
2.3 — Cálculo de CadastralArea con PostGIS	La función ST_Area en EPSG:4326 devuelve metros cuadrados esféricos. Verificar dividir por 10000 correctamente. Testear con un lote de área conocida antes de deploy.
3.3 — Bug SupplyApprovals	Después del fix, ejecutar un script de verificación que compare la suma de PlannedTotal de todos los LaborSupply de una OT con la suma de TotalCalculated en SupplyApprovals para detectar inconsistencias históricas.
3.2 — FK self-referential en Labor	PlannedLaborId es nullable. Agregar índice: CREATE INDEX IF NOT EXISTS ix_labors_planned ON "Labors"("PlannedLaborId") WHERE "PlannedLaborId" IS NOT NULL;
General — Multi-tenant	El TenantSessionInterceptor ya aísla las queries. Al clonar labores (3.2, 4.1), verificar que el nuevo registro hereda el TenantId correcto (TenantEntity.TenantId). El interceptor lo maneja, pero forzar explícitamente en el código de clonación.

Orden de Implementación Sugerido
Prioridad por impacto / riesgo:
•	Sprint 1 (Bugs críticos): 3.3 (bug insumos OT) + 2.3 (bug CadastralArea = 0) + 5.1 (dark mode). Bajo riesgo, alto impacto visual.
•	Sprint 2 (Validaciones): Bloque 1 completo (1.1, 1.2, 1.3). Requieren coordinación frontend + backend pero bajo riesgo.
•	Sprint 3 (GIS): Bloque 2 completo (2.1 buscador ciudad, 2.2 crear lote desde GIS, 2.4 variación superficie, 2.5 historial).
•	Sprint 4 (OT & Trazabilidad): 3.1 (selector campaña en OT) + 3.2 (ejecución sueltas con trazabilidad). Mayor complejidad.
•	Sprint 5 (Exportación HTML): Bloque 4 completo. Mayor superficie técnica, requiere QA extenso.

GestorOT Fix Plan — generado por análisis de código fuente del repositorio Gestor-OT-main
