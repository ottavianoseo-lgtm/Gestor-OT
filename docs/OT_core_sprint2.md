•	Sprint 2 (Validaciones): Bloque 1 completo (1.1, 1.2, 1.3). Requieren coordinación frontend + backend pero bajo riesgo.
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