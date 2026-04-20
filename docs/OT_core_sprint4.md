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

“Usar siempre la documentación oficial más reciente de .NET (Microsoft Learn / dotnet/docs) como fuente primaria. Evitar ejemplos obsoletos o versiones anteriores.”

Y si usás Context7, asegurate de:

Indexar dotnet/docs
Priorizar esa fuente sobre otras
(Ideal) filtrar por versión → .NET 10