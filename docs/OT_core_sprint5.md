
BLOQUE 4  Exportación e Integraciones
•	Sprint 5 (Exportación HTML): Bloque 4 completo. Mayor superficie técnica, requiere QA extenso.

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