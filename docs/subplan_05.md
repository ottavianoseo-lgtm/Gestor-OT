Sub-Plan 05 — Campañas, OT UI y Rotaciones
Sprint 3  ·  Semana 3  ·  Prioridad: MEDIA

Este sub-plan es mayormente independiente de los anteriores. Puede iniciarse en paralelo al Sprint 2, aunque se recomienda hacerlo después para no mezclar PRs. Incluye cambios en Campañas, la UI de consolidación de OTs, y el panel de Rotaciones.

#	Tarea	Archivo(s)	Est.
1	Campañas: eliminar campos en creación	Campanias.razor, CampaignsController.cs	3h
2	Campañas: selector de temporadas predefinidas	Campanias.razor, CampaignsController.cs	4h
3	WorkOrderDetail: UI consolidación planeado vs. ejecutado	WorkOrderDetail.razor	6h
4	Rotaciones: panel/drawer desde modal de Lotes	Lotes.razor	8h


Tarea 1 — Campañas: Simplificar Creación
Backend: CampaignsController.cs
•	En POST api/campaigns (CreateCampaign): eliminar el bloque if (dto.Fields != null) y su procesamiento
•	Los campos se auto-calculan vía RecalculateFieldHectares() que ya se invoca al agregar lotes
•	Mantener POST api/campaigns/{id}/fields para compatibilidad — solo marcarlo internamente como deprecated

Frontend: Campanias.razor
•	En el modal de creación, simplificar el formulario a: Nombre, Estado, Presupuesto, toggle IsActive
•	Eliminar el bloque de gestión de fechas de inicio/fin libres
•	La gestión de Campos se mantiene como funcionalidad secundaria en el modal de detalle


Tarea 2 — Campañas: Selector de Temporadas Predefinidas
Backend — Nuevo Endpoint: GET api/campaigns/available-seasons
Genera dinámicamente las temporadas disponibles:
•	Calcular desde 3 años atrás hasta 2 años adelante
•	Formato de nombre: '24/25', '25/26', '26/27', etc.
•	Fechas estándar: inicio 01/06/año, fin 30/06/año+1
•	Verificar cuáles ya existen en DB y marcarlas como existentes en el response

Ejemplo de response:
[
  { "name": "24/25", "startDate": "2024-06-01", "endDate": "2025-06-30", "alreadyExists": false },
  { "name": "25/26", "startDate": "2025-06-01", "endDate": "2026-06-30", "alreadyExists": true },
  { "name": "26/27", "startDate": "2026-06-01", "endDate": "2027-06-30", "alreadyExists": false }
]

Frontend: Campanias.razor
•	Reemplazar el formulario libre de fechas por un <Select> que muestra las temporadas disponibles (las no creadas aún)
•	Al seleccionar una temporada: rellenar automáticamente Name y fechas con los valores estándar
•	El usuario solo puede personalizar el Nombre (pre-completado) y el Presupuesto
•	En CampaignSelector.razor: listar campañas ordenadas por temporada, con la activa destacada


Tarea 3 — WorkOrderDetail: UI de Consolidación
Archivo: src/GestorOT.Client/Pages/WorkOrderDetail.razor
Reestructurar la pantalla en tres secciones (tabs o cards). Mantener la lógica existente (GetSupplyDetails(), ConsolidateSupplies()).

Sección	Contenido
Resumen General (KPIs)	Cards: total Ha planeadas, total Ha ejecutadas, % cumplimiento, insumos planeados vs. usados (unidades y costo estimado)
Labores	Tabla: Lote, Tipo, Estado (badge), Ha Plan., Ha Real., Diferencia (verde/rojo), Responsable, Fecha
Insumos Consolidados	Tabla: Insumo, Unidad, Total Planeado, Total Usado, Delta, % Utilización. Subtotales por insumo

Reglas de color para diferencias:
•	Diferencia negativa (usó MENOS de lo planeado) → color verde (ahorro)
•	Diferencia positiva (usó MÁS de lo planeado) → color rojo (exceso)
•	Diferencia cero → color neutro
•	Badges de estado: Planned=azul, AwaitingValidation=amarillo, Validated=naranja, Realized=verde


Tarea 4 — Rotaciones: Panel desde Modal de Lotes
Archivo: src/GestorOT.Client/Pages/Lotes.razor
Actualmente GoToRotations() en línea 436 navega a una página separada. Cambiar a un Drawer lateral:

•	Reemplazar NavigationManager.NavigateTo() por apertura de <Drawer> de AntDesign
•	El Drawer abre a la derecha con ancho amplio (80-90% del viewport)

Contenido del Drawer — Vista de Timeline de Rotaciones
•	Eje X: campañas (columnas), con scroll horizontal si hay muchas
•	Eje Y: el lote seleccionado (filas por cultivo/período)
•	Cada celda campaña×cultivo permite: + agregar cultivo, × eliminar cultivo, ver duración
•	Al agregar cultivo: mostrar selector de meses del año para definir período de aplicación
•	Los endpoints de RotationsController.cs ya existen — no se requieren cambios en backend

Validación de Solapamiento
•	El backend ya valida solapamientos en IAgronomicValidationService
•	Cambio visual: mostrar el error de solapamiento como toast/banner NO INTRUSIVO en lugar de modal bloqueante
•	Ejemplo de mensaje: 'Tu selección se solapa con la rotación de Maíz y Papa (Jun-Sep 2024)'

⚠ Nota de diseño
Seguir el diseño visual de referencia mencionado en el plan original (panel tipo 'Rotations' con scroll horizontal). Si no hay mockup disponible, implementar una grilla responsive con AntDesign Table + scroll horizontal habilitado.

