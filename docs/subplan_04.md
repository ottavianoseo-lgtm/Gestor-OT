Sub-Plan 04 — Módulo de Labores: Frontend
Sprint 2 — Fin  ·  Semana 2  ·  Prioridad: MEDIA-ALTA

Este sub-plan cubre todos los cambios de UI en componentes Blazor relacionados con labores. Requiere que los Sub-Planes 02 y 03 estén completados.

⚠ Prerequisito
Sub-Planes 02 y 03 completados. Los endpoints de submit-for-validation, PATCH priority y GET unassigned deben estar disponibles antes de implementar el UI.

#	Tarea	Archivo	Est.
1	LaborExecution.razor — campos reales + integración validación	LaborExecution.razor	5h
2	LaborEditorForm.razor — sección meteorológica	LaborEditorForm.razor	3h
3	LaborEditorForm.razor — campo retiro de insumos	LaborEditorForm.razor	2h
4	LaboresSueltas.razor — reordenamiento por prioridad	LaboresSueltas.razor	4h
5	LaborEditorForm.razor — botón 'Enviar para validación'	LaborEditorForm.razor	2h


Tarea 1 — LaborExecution.razor (Página Pública del Magic Link)
Archivo: src/GestorOT.Client/Pages/LaborExecution.razor
Esta página ya existe y maneja el flujo público. Los cambios son:
•	Agregar campo: Hectáreas Reales ejecutadas (InputNumber con validación > 0)
•	Agregar sección: Insumos Reales — tabla editable con cantidad real por insumo (pre-cargada con los insumos planeados)
•	Botón 'Confirmar Ejecución' → llama POST api/labors/{id}/realize con los datos del formulario
•	Al confirmar: mostrar mensaje de éxito y marcar el token como usado (IsUsed = true en backend)
•	Manejo de errores: token expirado, token ya usado, labor no en estado AwaitingValidation
•	Esta página no usa [Authorize] — es pública. Usar IgnoreQueryFilters en el contexto de tenant.


Tarea 2 — Sección Meteorológica en LaborEditorForm
Archivo: src/GestorOT.Client/Components/LaborEditorForm.razor
Agregar una sección colapsable 'Condiciones Climáticas' al final del formulario:
•	Temperatura (°C) — InputNumber, opcional
•	Humedad (%) — InputNumber 0-100, opcional
•	Velocidad del Viento (km/h) — InputNumber, opcional
•	Dirección del Viento — Select con valores: N, NE, E, SE, S, SO, O, NO
•	Esta sección NO bloquea el guardado — es completamente opcional
•	Los valores se serializan a JSON en WeatherLogJson: JsonSerializer.Serialize(weatherLog) antes de enviar el DTO
•	Al cargar la labor, deserializar WeatherLogJson para pre-poblar los campos


Tarea 3 — Campo Retiro de Insumos
Archivo: src/GestorOT.Client/Components/LaborEditorForm.razor
•	Agregar TextArea con label 'Retiro de Insumos' debajo del bloque de insumos
•	Solo mostrar cuando Mode = Planned o Status = Planned
•	Binding: @bind-Value='_model.SupplyWithdrawalNotes'
•	Placeholder: 'Ej: Retirar de depósito norte el 15/05...'
•	Máximo 1000 caracteres recomendado


Tarea 4 — Reordenamiento por Prioridad en LaboresSueltas
Archivo: src/GestorOT.Client/Pages/LaboresSueltas.razor
•	Agregar columnas en la tabla: Prioridad (número editable) y Fecha Estimada
•	Agregar botones de flecha ↑ ↓ por fila para reordenar (alternativa: input numérico directo)
•	Al cambiar el orden: llamar PATCH api/labors/{id}/priority con el nuevo valor
•	Agregar control de ordenamiento en el header: botones 'Ordenar por Prioridad' / 'Ordenar por Fecha'
•	Los cambios de prioridad deben ser optimistas (reflejar en UI antes de confirmar el API call)


Tarea 5 — Botón 'Enviar para Validación'
Archivo: src/GestorOT.Client/Components/LaborEditorForm.razor (o en la vista de detalle)
•	Mostrar el botón solo cuando Mode = Planned y Status = Planned
•	Al hacer clic: llamar POST api/labors/{id}/submit-for-validation
•	Mostrar la URL del Magic Link retornada por el endpoint en un modal o toast copiable
•	El usuario puede copiar y compartir el enlace con el ejecutor de la labor
•	Después del envío, actualizar el Status en UI a AwaitingValidation (badge amarillo)

Badges de Estado para Labores
Colores de referencia para los badges de estado en toda la interfaz:
Estado	Color Badge	Contexto
Planned	Azul (#1890FF)	Labor planificada, sin ejecutar
AwaitingValidation	Amarillo (#FAAD14)	Enviada al ejecutor
Validated	Naranja (#FA8C16)	Confirmada por ejecutor
Realized	Verde (#52C41A)	Ejecutada completamente

