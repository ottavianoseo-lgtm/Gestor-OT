# Sprint 04 - Labores, validaciones visuales y CampaignLotId

## Objetivo

Corregir creación y edición de labores para que usen siempre la relación correcta `CampaignLotId`, tengan validaciones claras y no fallen con lotes presentes en varias campañas.

## Rama sugerida

`fix/s04-labores-validaciones`

## Bugs cubiertos

- Campos obligatorios no se marcan en rojo.
- Lote con varias campañas puede tomar campaña incorrecta.
- Guardar labor puede fallar silenciosamente.
- Hectáreas superiores al lote bloquean cuando deberían advertir.
- Planeamiento Original falla parcialmente por reglas compartidas de labor.

## Archivos principales

- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- `src/GestorOT.Shared/Dtos/LaborDto.cs`
- `src/GestorOT.Shared/Validation/ApiRequestDtos.cs`, si aplica
- Servicios de validación si existen
- `src/GestorOT.Domain/Entities/Labor.cs`
- `src/GestorOT.Domain/Entities/CampaignLot.cs`

## Regla funcional

Una labor se hace sobre un lote en una campaña. Por eso debe guardar `CampaignLotId`. `LotId` puede quedar como dato derivado o redundante, pero no debe ser la clave de contexto.

## Tareas técnicas

### 1. Frontend - selección de lote

1. El selector de lote debe usar `CampaignLotDto`.
2. Al seleccionar lote:
   - setear `_model.CampaignLotId = campaignLot.Id`;
   - setear `_model.LotId = campaignLot.LotId`;
   - setear hectáreas iniciales con `ProductiveArea`;
   - si `ProductiveArea == 0`, usar `CadastralArea` con warning.
3. Si no hay campaña seleccionada, no permitir crear labor.
4. Si campaña bloqueada, solo lectura.

### 2. Backend - contrato estricto

En `LaborsController.CreateLabor` y `UpdateLabor`:

1. Requerir `CampaignLotId`.
2. Validar que existe.
3. Validar que pertenece a la campaña actual o enviada.
4. Obtener `LotId` desde `CampaignLot`.
5. No usar `FirstOrDefault` por `LotId` si hay múltiples campañas.
6. Si llega legacy `LotId` sin `CampaignLotId`, devolver error claro:
   - "Debe enviar CampaignLotId porque el lote puede pertenecer a múltiples campañas".

### 3. Validaciones visuales

En Blazor:

1. Usar validación por campo.
2. Marcar en rojo:
   - lote;
   - actividad;
   - tipo de labor;
   - hectáreas;
   - fecha;
   - responsable;
   - estado;
   - insumo/dosis si corresponde.
3. Mostrar mensaje debajo del campo.
4. Mantener toast solo como resumen, no como única validación.

### 4. Validaciones backend

API debe devolver errores estructurados:

- `field`
- `message`
- `code`

Errores mínimos:

- `campaignLotId.required`
- `campaignLotId.notFound`
- `activity.required`
- `laborType.required`
- `hectares.required`
- `hectares.invalid`
- `date.required`
- `contact.required`

### 5. Hectáreas mayores

1. Si hectáreas superan superficie productiva:
   - frontend muestra warning;
   - usuario puede confirmar;
   - backend no bloquea por esta razón.
2. Si hectáreas son cero o negativas:
   - bloquear.

### 6. Insumos

1. Si se agrega insumo, dosis y unidad deben ser válidas.
2. Calcular totales consistentemente:
   - planificado: `PlannedDose * PlannedHectares`;
   - realizado: `RealDose * RealHectares`.
3. No borrar insumos existentes por error si el usuario no tocó la sección.

## No hacer en este sprint

- No implementar biblioteca de adjuntos.
- No corregir estrategias completas.
- No modificar modelo de Planeamiento Original salvo lo necesario para labores base.

## Pruebas manuales

### Caso 1 - Labor incompleta

1. Abrir nueva labor.
2. Guardar sin datos.

Resultado esperado:

- No guarda.
- Campos obligatorios en rojo.
- Mensajes debajo del campo.

### Caso 2 - Lote en dos campañas

1. Lote A asignado a campaña 23/24 y 26/27.
2. Seleccionar campaña 26/27.
3. Crear labor para Lote A.

Resultado esperado:

- La labor usa `CampaignLotId` de campaña 26/27.
- No toma la campaña 23/24.

### Caso 3 - Hectáreas superiores

1. Lote productivo 100 ha.
2. Crear labor con 101 ha.

Resultado esperado:

- Advierte.
- Permite confirmar.
- Guarda.

### Caso 4 - Backend protegido

1. Enviar request sin `CampaignLotId`.

Resultado esperado:

- API rechaza con error claro.

## Criterios de aceptación

- No se crean labores sin `CampaignLotId`.
- UI muestra errores por campo.
- Lotes multicampaña funcionan.
- Hectáreas superiores no bloquean.
- El código compila.

## Prompt corto para DeepSeek

Implementá solo Sprint 04. Corregí LaborEditorForm y LaborsController para que toda labor use CampaignLotId, tenga validación visual por campo y backend robusto. No infieras CampaignLotId por LotId. Hectáreas superiores a productiva deben advertir pero permitir. No implementes adjuntos ni estrategias todavía.
