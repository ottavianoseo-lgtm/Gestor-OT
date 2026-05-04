# Sprint 01 - Campañas, selector global, estados y refresco

## Objetivo

Corregir el contexto global de campaña. Hoy una campaña puede existir en la pantalla de Campañas pero no aparecer en el selector global. Esto bloquea la creación de OT, labores, estrategias y planeamiento.

## Rama sugerida

`fix/s01-campanias-selector`

## Bugs cubiertos

- Campaña `28/29` no aparece en el selector.
- Nueva campaña requiere `Ctrl + F5`.
- Campañas bloqueadas no se pueden consultar desde selector.
- Cambio de estado de campaña no se persiste correctamente.
- El sistema confunde `Status` e `IsActive`.

## Archivos principales

- `src/GestorOT.Client/Layout/CampaignSelector.razor`
- `src/GestorOT.Client/Services/CampaignState.cs`
- `src/GestorOT.Client/Pages/Campanias.razor`
- `src/GestorOT.Api/Controllers/CampaignsController.cs`
- `src/GestorOT.Shared/Dtos/CampaignDto.cs`
- `src/GestorOT.Domain/Entities/Campaign.cs`

## Diagnóstico esperado

El selector actual no debe depender solo de `api/campaigns/active`, porque ese endpoint excluye campañas bloqueadas y posiblemente campañas futuras/inactivas. El selector global debe ser de navegación/contexto, no solo de campañas modificables.

## Diseño funcional esperado

### Endpoint nuevo sugerido

Crear un endpoint específico:

`GET /api/campaigns/selector`

Debe devolver campañas de la empresa con:

- `Id`
- `Name`
- `StartDate`
- `EndDate`
- `Status`
- `IsActive`

### Reglas

- Campaña `Active`: seleccionable y editable.
- Campaña `Locked`: seleccionable pero solo lectura.
- Campaña `Inactive`: visible en selector global si existe; no asignable a nuevos lotes/labores salvo decisión explícita.
- No ocultar campañas bloqueadas del selector.
- No auto-seleccionar campañas nuevas salvo que sea la única campaña.
- El selector debe refrescar sin `Ctrl + F5`.

## Tareas técnicas

### 1. Backend

1. Agregar endpoint `GET /api/campaigns/selector`.
2. Mantener `GET /api/campaigns/active` si otros módulos dependen de campañas asignables.
3. Corregir `UpdateCampaignStatus`:
   - debe asignar `campaign.Status = newStatus.ToString()`;
   - si corresponde, ajustar `IsActive`;
   - guardar cambios.
4. Revisar `UnlockCampaign` para que no rompa regla funcional.
5. Agregar DTO liviano si `CampaignSummaryDto` no alcanza.

### 2. Estado compartido

1. En `CampaignState`, agregar evento para refresco de campañas:
   - `OnCampaignsChanged`.
   - método `NotifyCampaignsChanged`.
2. Mantener evento de cambio de campaña seleccionada.
3. Agregar propiedad calculada o método para saber si la campaña actual está bloqueada.

### 3. Selector global

1. Cambiar carga del selector a `api/campaigns/selector`.
2. Mostrar etiquetas:
   - Activa.
   - Bloqueada.
   - Inactiva.
3. Permitir seleccionar bloqueada.
4. Si está bloqueada, mostrar indicador visual `Solo lectura`.
5. Recargar listado cuando `CampaignState.OnCampaignsChanged` se dispare.

### 4. Pantalla Campañas

Después de crear/editar/bloquear/desbloquear/eliminar:

1. Llamar `LoadData()`.
2. Llamar `CampaignState.NotifyCampaignsChanged()`.
3. Si la campaña seleccionada fue modificada, actualizar `CurrentCampaign`.

## No hacer en este sprint

- No corregir estrategias.
- No corregir labores.
- No implementar adjuntos.
- No rediseñar toda la UI.
- No eliminar `IsActive`.

## Pruebas manuales

### Caso 1 - Crear campaña futura

1. Ir a Campañas.
2. Crear `Campaña 2028/2029`.
3. Guardar.
4. Abrir selector global.

Resultado esperado:

- Aparece sin `Ctrl + F5`.
- Puede seleccionarse.
- Se visualiza estado.

### Caso 2 - Bloquear campaña

1. Bloquear campaña activa.
2. Abrir selector global.
3. Seleccionarla.

Resultado esperado:

- Sigue apareciendo.
- Se puede seleccionar.
- El contexto indica solo lectura.

### Caso 3 - Refresco automático

1. Crear campaña nueva.
2. Sin refrescar navegador, abrir selector.

Resultado esperado:

- La campaña aparece.

### Caso 4 - Cambio de estado

1. Bloquear campaña.
2. Recargar datos.
3. Ver estado en backend/listado.

Resultado esperado:

- El estado cambió realmente.

## Criterios de aceptación

- El selector global no depende de `active`.
- Campañas bloqueadas son visibles.
- Campañas nuevas aparecen sin refresco manual.
- `UpdateCampaignStatus` persiste estado.
- El código compila.
- Tests existentes pasan o se documentan fallas previas.

## Prompt corto para DeepSeek

Implementá solo Sprint 01. Corregí selector global de campañas, refresco automático y persistencia de estados. No toques lotes, labores, estrategias ni adjuntos salvo referencias mínimas para compilar. Entregá PR chico con pruebas manuales: crear campaña futura, verla en selector sin refresh, bloquear campaña y seleccionarla como solo lectura.
