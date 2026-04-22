GestorOT — Índice Maestro de Sub-Planes
Versión 1.0  ·  Abril 2026

Este documento es el punto de entrada para el agente de desarrollo. El plan general fue dividido en 5 sub-planes independientes, cada uno orientado a una etapa de trabajo concreta. El agente debe ejecutarlos en el orden indicado respetando las dependencias.

Visión General del Sistema
Ítem	Detalle
Proyecto	GestorOT — Módulo de Órdenes de Trabajo y Labores Agrícolas
Stack	.NET 10 · Blazor WebAssembly · EF Core · PostgreSQL (Supabase) · Ant Design Blazor
Arquitectura	Clean Architecture · Multi-tenant · CQRS parcial · API REST + Blazor Client
Total de cambios	11 cambios estructurales + 3 correcciones de UI
Duración estimada	3 semanas (3 sprints)

Mapa de Sub-Planes
#	Sub-Plan	Categorías	Sprint	Prioridad
00	Índice Maestro (este doc)	—	—	Referencia
01	Correcciones Críticas de UI	D1, D2, B1, A1-parcial	Sprint 1	🔴 Alta
02	Domain & Infraestructura	A1 (Domain + Migración)	Sprint 2 inicio	🔴 Alta
03	Módulo de Labores — Backend	A1-API, A2, A3, A4-backend	Sprint 2 medio	🔴 Alta
04	Módulo de Labores — Frontend	A1-UI, A2-UI, A3-UI, A4-UI, B3	Sprint 2 fin	🟡 Media
05	Campañas, OT y Rotaciones	B2, C1, C2, E1	Sprint 3	🟡 Media

Orden de Ejecución y Dependencias
•	Sub-Plan 01 es independiente. El agente puede ejecutarlo sin bloquearse en nada previo.
•	Sub-Plan 02 debe ejecutarse antes que 03 y 04 (crea las entidades y migraciones de las que dependen los demás).
•	Sub-Plan 03 depende de Sub-Plan 02 (necesita LaborStatus enum y los nuevos campos en la DB).
•	Sub-Plan 04 depende de Sub-Plan 03 (consume los endpoints creados en 03).
•	Sub-Plan 05 es independiente de 02-04 pero debe ir al final para no mezclar PRs.

Índice de Archivos Globales
Archivos a MODIFICAR
Archivo	Capa	Sub-Plan(s)
src/GestorOT.Domain/Entities/Labor.cs	Domain	02
src/GestorOT.Domain/Enums/LaborMode.cs	Domain	02
src/GestorOT.Shared/Dtos/LaborDto.cs	Shared	02, 04
src/GestorOT.Infrastructure/Data/Configurations/LaborConfiguration.cs	Infrastructure	02
src/GestorOT.Api/Controllers/LaborsController.cs	API	03
src/GestorOT.Api/Controllers/CampaignsController.cs	API	05
src/GestorOT.Client/Layout/MainLayout.razor	Client	01
src/GestorOT.Client/Components/LaborEditorForm.razor	Client	04
src/GestorOT.Client/Pages/OrdenesTrabajos.razor	Client	01
src/GestorOT.Client/Pages/WorkOrderDetail.razor	Client	05
src/GestorOT.Client/Pages/Campanias.razor	Client	05
src/GestorOT.Client/Pages/Lotes.razor	Client	05
src/GestorOT.Client/Pages/LaborExecution.razor	Client	04
src/GestorOT.Client/Pages/LaboresSueltas.razor	Client	04
src/GestorOT.Client/wwwroot/css/app.css	Client	01

Archivos a CREAR
Archivo	Capa	Sub-Plan
src/GestorOT.Domain/Enums/LaborStatus.cs	Domain	02
src/GestorOT.Infrastructure/Data/Migrations/AddLaborPriorityAndWithdrawal.cs	Infrastructure	02

