# Revisión integral — Gestor OT — rama `fix/bugs-revision-gestor-ot`

## Alcance

Repositorio revisado: `ottavianoseo-lgtm/Gestor-OT`  
Rama revisada: `fix/bugs-revision-gestor-ot`  
Base comparada: `main`  
Contexto funcional usado: `Revisión Gestor OT(5).pdf` y `spectOT(5).md`

## Resultado ejecutivo

La rama contiene avances importantes y corrige varios puntos reportados, pero **no debería mergearse todavía**. Persisten defectos funcionales en el flujo de estrategias, adjuntos, estado real de OT y seguridad/tenant de archivos. Además faltan pruebas de regresión específicas para los bugs reportados.

## Bugs / mejoras que parecen corregidas o encaminadas

1. **Selector global de campañas**
   - Existe endpoint `api/campaigns/selector` que devuelve todas las campañas.
   - El componente `CampaignSelector.razor` consume ese endpoint y distingue campañas bloqueadas como solo lectura.
   - `Campanias.razor` llama `CampaignState.NotifyCampaignsChanged()` después de crear, modificar, bloquear/desbloquear o eliminar campañas, lo que debería evitar el Ctrl+F5.

2. **Nueva campaña aparece en selector global**
   - La pantalla de campañas refresca datos y notifica al selector global después de guardar.

3. **Crear OT abre modal si hay campaña activa seleccionada**
   - `OrdenesTrabajos.razor` valida campaña seleccionada/no bloqueada y luego abre `_modalVisible = true`.

4. **Campañas asignadas por lote**
   - `Lotes.razor` muestra historial de superficies por campaña al expandir el lote.
   - El selector para asignar campaña al lote filtra campañas ya asignadas.
   - Hay acceso a rotaciones por campaña/lote desde la grilla expandida y desde el modal.

5. **Estrategias: actividad única y visualización de labores reales**
   - `Estrategias.razor` ya no muestra actividad por cada labor de estrategia.
   - Muestra la actividad de la estrategia, los nombres reales de labores y los insumos/dosis.
   - El modal de aplicar estrategia incluye el nombre de la estrategia en el título.

6. **Planeamiento Original no debe crear Realizadas**
   - `PlaneamientoOriginal.razor` invoca `LaborEditorForm ForceOriginalPlan="true"` y `StrategyLaborWizard ForceOriginalPlan="true"`.
   - `LaborsController.CreateLabor` bloquea `IsOriginalPlan` si `Status` o `Mode` no son `Planned`.

7. **Adjuntos antes de guardar labor**
   - `LaborAttachments.razor` permite subir archivo sin `LaborId` y guarda IDs pendientes.
   - `FilesController.Upload` permite `laborId` opcional y vincula si existe.

## Defectos encontrados que requieren corrección

### 1. Estrategias: “mantener separación de fechas” no está implementado realmente

**Impacto:** Alto. Afecta uno de los bugs principales reportados.

En `StrategyLaborWizard.razor`, la vista previa se genera con `DateTime.Today` como base silenciosa y cada `DatePicker` solo hace `@bind-Value="preview.Date"`. No existe handler que, al modificar una fecha, recalcule las demás fechas manteniendo los offsets cuando el checkbox está activo.

**Resultado esperado:**

- No debe haber una “Fecha Base” precargada como campo obligatorio.
- La vista previa puede sugerir fechas, pero deben ser editables.
- Si `Mantener separación de fechas` está activo y se cambia una fecha de una labor, el grupo de labores relacionadas debe recalcularse respetando los offsets de la estrategia.
- Si se desactiva el checkbox, cada fecha queda independiente.

### 2. Estrategias: validación de rotaciones se parsea mal y puede no bloquear conflictos

**Impacto:** Alto. La spec dice que si existe rotación activa y la actividad de la labor no coincide, debe bloquearse.

`StrategyLaborWizard.razor` llama `api/labors/validate-rotation-activity`, pero trata la respuesta como string en `CheckActivityWarning`. El backend retorna un objeto `LaborActivityValidationResult`. Además se llama dos veces por cada preview row.

**Riesgo:**

- Mostrar JSON crudo como advertencia.
- No detectar correctamente `Severity == "Error"`.
- Permitir crear labores con actividad de estrategia en conflicto con rotaciones.

### 3. Estrategias: hectáreas en vista previa no fuerzan 2 decimales

**Impacto:** Medio.

La vista previa usa `InputNumber` para `preview.Hectares`, pero no se ve formato/precisión explícita a 2 decimales.

### 4. Adjuntos: biblioteca no está disponible al editar una labor existente

**Impacto:** Medio/Alto.

El botón “Biblioteca” solo aparece cuando `!HasLaborId`. La spec indica que se debe poder seleccionar un archivo existente desde labores u órdenes de trabajo. Actualmente, en una labor ya creada se puede subir archivo nuevo, pero no seleccionar uno existente de la biblioteca.

### 5. Adjuntos: no hay flujo al cancelar una labor nueva con archivos subidos

**Impacto:** Medio.

La spec pide preguntar al usuario si elimina o conserva archivos subidos por primera vez cuando cancela una labor nueva. El componente actual mantiene archivos como `FileAsset` en biblioteca, pero no implementa confirmación ni limpieza de no vinculados.

### 6. Adjuntos: `LinkPendingFiles` oculta errores

**Impacto:** Medio.

`LaborAttachments.LinkPendingFiles` atrapa excepciones, escribe en consola y no devuelve resultado. Si el guardado de la labor fue exitoso pero falla el vínculo de adjuntos, el usuario puede creer que quedó todo guardado.

### 7. OT: estado elegido por usuario no se vincula correctamente al `WorkOrderStatusId`

**Impacto:** Alto.

`WorkOrdersController.CreateWorkOrder` calcula `statusName` desde `dto.Status`, pero siempre setea `WorkOrderStatusId = defaultStatus?.Id`. Si el usuario elige un estado distinto al default, el string `Status` queda con un valor y el FK queda apuntando al estado default. Como la edición/bloqueo depende de `WorkOrderStatus`, esto puede producir comportamiento inconsistente.

### 8. Seguridad/tenant: `FilesController` usa `FindAsync` en entidades sensibles

**Impacto:** Alto si `FileAsset` y/o `Labor` son tenant-scoped.

`FilesController.Download`, `Delete` y `LinkFiles` usan `FindAsync`. En este proyecto ya existía una regla técnica documentada: evitar `FindAsync` en PUT/DELETE o recursos sensibles porque puede saltear filtros globales de tenant. Debe reemplazarse por `FirstOrDefaultAsync` con filtros respetados y validaciones explícitas.

### 9. Planeamiento Original: UI todavía muestra conceptos de realizado/desvío

**Impacto:** Bajo/Medio.

Aunque el backend bloquea Realizadas en Planeamiento Original, la tabla muestra columnas “Ha Real”, “Diff” y lógica visual para `Realized`. No rompe el guardado, pero contradice la idea de línea base planeada e inmutable y puede confundir al usuario.

## Prioridad de corrección

1. Corregir flujo de estrategia: fechas, rotación, decimales y tests.
2. Corregir OT status FK y tests.
3. Corregir adjuntos: biblioteca en edición, cancelación con archivos pendientes, errores visibles.
4. Endurecer seguridad/tenant de archivos.
5. Limpiar UX de Planeamiento Original.

## Pruebas mínimas requeridas antes de merge

- `dotnet build` de la solución completa.
- `dotnet test` de todo el proyecto de tests.
- Tests unitarios/integración para:
  - `CreateLabor` bloquea `IsOriginalPlan` + `Realized`.
  - `CreateBulkFromStrategy` bloquea conflictos de rotación.
  - `StrategyLaborWizard` mantiene separación de fechas cuando corresponde.
  - `WorkOrdersController.CreateWorkOrder` usa el `WorkOrderStatusId` del estado elegido.
  - `FilesController` no permite acceso cross-tenant y no vincula archivos inexistentes.
- Prueba manual UI para los 23 bugs reportados en PDF.
