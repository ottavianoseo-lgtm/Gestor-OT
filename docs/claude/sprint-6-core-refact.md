# Sprint 6 — Refactor Core, Persistencia y Estabilidad de Labores
**GestorMax · Gestor OT** | Abril 2026 | Prioridad: CRÍTICA

---

## Objetivo

Resolver fallos críticos de persistencia de datos en el core (Lotes), estabilizar la experiencia de usuario en la creación/edición de labores, mejorar la identificación de Órdenes de Trabajo (OT) y estandarizar la categorización de prioridades.

---

## Issues a resolver

### #20 — Fallo de persistencia en Área Catastral
**Severidad:** ALTO (Data Integrity)  
**Contexto:** Al modificar el área catastral de un lote en la gestión de lotes, los cambios no se guardan correctamente en la base de datos a pesar de no arrojar errores visuales.
**Tareas:**
1. Revisar `LotsController.cs` y el mapeo en `UpdateLot`.
2. Asegurar que el DTO `LotDto` y la entidad `Lot` estén sincronizados respecto a `CadastralArea`.
3. Validar que la lógica de cálculo automático por WKT no sobreescriba valores manuales si estos han sido explícitamente editados.

---

### #21 — Identificación Human-Readable para OTs
**Severidad:** MEDIO (UX)  
**Contexto:** Los códigos de OT actuales son difíciles de identificar para el usuario. Se requiere una forma más amigable de reconocerlas.
**Tareas:**
1. Agregar campo opcional `Name` a la entidad `WorkOrder` y sus DTOs.
2. Actualizar el formulario de creación de OT para permitir ingresar este nombre.
3. Implementar lógica de fallback: si el `Name` está vacío, mostrar un nombre generado: `[OT-Número] - [Descripción] - [Campo]`.
4. Actualizar todos los selectores de OT (especialmente en `LaborEditorForm`) para mostrar este nuevo nombre/identificador.

---

### #22 — Jerarquía de Superficies (Productiva vs Catastral vs GIS)
**Severidad:** ALTO (Regla de Negocio)  
**Contexto:** Existe confusión en qué superficie usar para los cálculos de insumos y labores. La regla de oro es: **La superficie productiva definida en la relación Lote-Campaña es la fuente de verdad única para todas las cuentas.**
**Reglas a implementar:**
1. Al crear una labor, las hectáreas por defecto **deben** ser las de `CampaignLot.ProductiveArea`.
2. El área calculada por GIS (dibujo) **no debe** usarse como referencia para cálculos de labores/insumos en esta etapa; es solo un valor informativo.
3. Si un lote no tiene superficie productiva definida para la campaña actual, el sistema debe alertar al usuario, permitiéndole usar la Catastral como fallback inicial pero forzando la revisión.
4. Asegurar que al "Importar" lotes entre campañas, se mantenga la superficie productiva como referencia.

---

### #23 — Estabilización Crítica del Modal de Labores
**Severidad:** BLOQUEANTE (UX/Stability)  
**Contexto:** El editor de labores (`LaborEditorForm.razor`) presenta comportamientos erráticos:
- Desaparición de botones de Guardar/Cancelar.
- Pantallazos en blanco ("White screen of death") al completar datos.
- Bloqueo de la opción de adjuntos.
- Estado inconsistente al abrir/cerrar el modal repetidamente desde "Labor Suelta".
**Tareas:**
1. Auditar el ciclo de vida de Blazor en `LaborEditorForm` (especialmente `OnInitializedAsync` y `OnParametersSetAsync`).
2. Resolver posibles condiciones de carrera en la carga de catálogos y rotaciones.
3. Asegurar que el estado del formulario se resetee completamente al cerrar el modal.
4. Validar la consistencia de `LaborId` y `WorkOrderId` al abrir el componente.

---

### #24 — UX de Interacción y "Click Area" en Listas
**Severidad:** MEDIO (UX)  
**Contexto:** Los usuarios reportan dificultad para abrir labores creadas haciendo clic; parece que el área activa de clic es pequeña o inconsistente ("se mueve de lugar").
**Tareas:**
1. Revisar los templates de tabla en `LaboresSueltas.razor` y `WorkOrderDetail.razor`.
2. Envolver toda la fila o el componente de visualización en un área interactiva clara.
3. Mejorar el feedback visual (hover state) para indicar que el elemento es clickeable.

---

### #25 — Estandarización de Prioridad de Labores
**Severidad:** BAJO (Consistencia)  
**Contexto:** Actualmente la prioridad es un campo numérico libre. Se requiere normalizarla a valores predefinidos.
**Tareas:**
1. Definir enum `LaborPriority` con valores: `Baja`, `Regular`, `Alta`, `Urgente`.
2. Actualizar la entidad `Labor` y el DTO `LaborDto` para usar este enum.
3. Reemplazar el `InputNumber` de prioridad en `LaborEditorForm.razor` por un `Select` con las opciones del enum.

---

## Criterios de aceptación del Sprint 6

- [ ] Las modificaciones manuales en el Área Catastral persisten tras recargar la página.
- [ ] Las OTs muestran un nombre descriptivo en todos los selectores de la plataforma.
- [ ] **Prioridad de Superficie**: Todos los cálculos de labores e insumos utilizan la superficie productiva del lote en la campaña actual.
- [ ] **Independencia GIS**: El área dibujada en el mapa no altera los cálculos automáticos de hectáreas en las labores.
- [ ] El modal de labores funciona de manera estable, permitiendo guardar, cancelar y adjuntar archivos sin crasheos o pérdida de botones.
- [ ] Las labores en las listas son fáciles de abrir mediante un clic en cualquier parte de la fila/card.
- [ ] El campo de prioridad de labor es un selector con las opciones "Baja", "Regular", "Alta" y "Urgente".
