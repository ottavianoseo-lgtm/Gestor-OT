# GestorOT V2 - Sistema de Gestión de Órdenes de Trabajo

## Overview
GestorOT es un sistema de gestión de órdenes de trabajo agrícola con capacidades GIS (PostGIS) para la visualización y gestión de lotes agrícolas. Construido con .NET 10, Blazor WebAssembly, y AntDesign. Incluye CRUD completo para Campos, Lotes, Órdenes de Trabajo e Inventario, editor GIS con Leaflet.Draw, importación de GeoJSON/KML, y comparación de superficies.

## Project Architecture

### Solution Structure (Hosted Blazor)
- **GestorOT.Server** - ASP.NET Core backend con EF Core y Supabase/PostgreSQL
- **GestorOT.Client** - Blazor WebAssembly frontend con AntDesign
- **GestorOT.Shared** - DTOs, Enums, validaciones y JsonSerializerContext

### Key Technologies
- .NET 10.0
- Blazor WebAssembly (Interactive)
- AntDesign Blazor UI
- Entity Framework Core 10 con Npgsql
- NetTopologySuite para geometría GIS
- Supabase/PostgreSQL con PostGIS
- Leaflet.js + Leaflet.Draw para editor GIS

## Database Schema
- **Fields** - Campos agrícolas (Id, Name, TotalArea, CreatedAt)
- **Lots** - Lotes con geometría PostGIS (Id, FieldId, Name, Status, Geometry)
- **WorkOrders** - Órdenes de trabajo (Id, LotId, Description, Status, AssignedTo, DueDate)
- **Inventories** - Inventario con unidad dual (Id, Category, ItemName, CurrentStock, ReorderLevel, UnitA, UnitB, ConversionFactor)

## Environment Variables
- `SUPABASE_CONNECTION_STRING` - Cadena de conexión a Supabase PostgreSQL

## Running the Application
```bash
cd GestorOT/GestorOT && ASPNETCORE_ENVIRONMENT=Development dotnet run --urls "http://0.0.0.0:5000"
```

## Design System (Agrivant UI)
- Primary Color: #E74C3C (Rojo)
- Status Green: #2ECC71
- Status Gold: #F1C40F
- Surface Dark: #1E1E2E
- Background: #0f0f1a
- Glassmorphism: backdrop-filter blur(10px), rgba borders

## API Endpoints
### Fields
- `GET /api/fields` - Lista todos los campos
- `GET /api/fields/{id}` - Obtener campo por ID
- `POST /api/fields` - Crear campo
- `PUT /api/fields/{id}` - Actualizar campo
- `DELETE /api/fields/{id}` - Eliminar campo

### Lots
- `GET /api/lots` - Lista todos los lotes
- `GET /api/lots/{id}` - Obtener lote por ID
- `GET /api/lots/geojson` - Lotes en formato GeoJSON
- `POST /api/lots` - Crear lote
- `PUT /api/lots/{id}` - Actualizar lote (incluye geometría WKT)
- `DELETE /api/lots/{id}` - Eliminar lote

### Work Orders
- `GET /api/workorders` - Lista órdenes de trabajo
- `GET /api/workorders/{id}` - Obtener OT por ID
- `POST /api/workorders` - Crear OT
- `PUT /api/workorders/{id}` - Actualizar OT (status workflow)
- `DELETE /api/workorders/{id}` - Eliminar OT

### Inventory
- `GET /api/inventory` - Lista inventario
- `GET /api/inventory/{id}` - Obtener item por ID
- `POST /api/inventory` - Crear item
- `PUT /api/inventory/{id}` - Actualizar item
- `DELETE /api/inventory/{id}` - Eliminar item

### Dashboard
- `GET /api/dashboard/stats` - Estadísticas del dashboard
- `GET /api/dashboard/recent-orders` - Órdenes de trabajo recientes

## Client-Side Patterns
- Data loading in `OnInitializedAsync` with 15s CancellationToken timeout
- Source-generated JSON via `AppJsonSerializerContext.Default`
- Error display using AntDesign Alert components
- AntDesign IMessageService for toast notifications (fire-and-forget, no await)
- CRUD modals with AntDesign Modal + Form components
- Explicit StateHasChanged() in finally blocks for reliable UI updates

## CRUD Pattern (all pages)
- "Nuevo X" button opens create modal
- Table row has Edit (pencil) and Delete (trash) action buttons
- Delete uses Popconfirm for safety
- Modal form with validation before submit
- After save/delete: reload data from API and refresh table

## Inventory Dual Units (ADR-004)
- UnitA = Stock unit (e.g., Bidones, Bolsas)
- UnitB = Application unit (e.g., Litros, Kg)
- ConversionFactor: 1 UnitA = X UnitB
- Real-time conversion display in create/edit form
- Table shows both stock and equivalent application units

## Work Order Status Workflow
- Pending → InProgress → Completed
- Quick-action buttons in table: "Iniciar" (yellow), "Completar" (green)
- Completed orders show "Finalizada" tag
- KPI cards show count by status

## Map Features
- Leaflet map with Esri satellite tiles and reference layer
- Lot polygons colored by status (Green=#2ECC71 Active, Red=#E74C3C Inactive)
- Leaflet.Draw for polygon creation/editing
- GeoJSON coordinate conversion (lon/lat to lat/lon for Leaflet)
- preferCanvas: true for performance
- Legend with lot count
- Auto-fit to show all lots

## GIS Editor Features
- Draw polygon tool with Leaflet.Draw
- Save drawn geometry to orphan lots (lots without geometry)
- Orphan lot panel showing lots without geometry
- Import GeoJSON/KML files via file upload
- JS-side parsing of GeoJSON and KML formats
- Link imported polygons to existing lots
- Surface comparison: Fiscal Area (from Field) vs Drawn Area (from geometry)
- Delta calculation with percentage display

## JS Interop Pattern
- Uses global window.mapInterop object (not ES modules)
- Leaflet.Draw loaded from CDN after Leaflet
- Scripts loaded in App.razor
- Called via IJSRuntime.InvokeAsync("mapInterop.methodName", args)
- JSInvokable callbacks: OnLotSelected, OnPolygonDrawn, OnPolygonEdited, OnPolygonDeleted

## Work Planner Module
- WorkPlanner.razor: CSS Grid calendar (month/week views) showing OTs on PlannedDate
- Status color coding: Draft=#9B59B6, Pending=#E74C3C, Scheduled=#3498DB, InProgress=#F1C40F, Completed=#2ECC71, Cancelled=#95A5A6
- StockValidatorService: soft-commit inventory validation per WorkOrder
- IsoXmlExporterService: ISO 11783 TaskData.xml export in ZIP format
- API endpoints: POST validate-stock, POST reserve-stock, GET export-isoxml

## Pages
- `/` - Dashboard (KPIs, recent orders)
- `/fields` - Campos CRUD
- `/lots` - Lotes CRUD
- `/workorders` - Órdenes de Trabajo CRUD con workflow de estado
- `/planner` - Work Planner calendario visual de OTs
- `/mapa` - Explorador GIS con editor de polígonos
- `/inventory` - Inventario con unidad dual

## Database Migration
- Startup migration in Program.cs ensures schema consistency via raw SQL (ALTER TABLE ADD COLUMN IF NOT EXISTS, CREATE TABLE IF NOT EXISTS)
- The execute_sql_tool and app connection go to the SAME Supabase database (via pooler)
- Inventory entity has nullable UnitA/UnitB properties with `?? ""` coalescing in LINQ projections

## Recent Changes
- 2026-02-05: Inicialización del proyecto con arquitectura Hosted Blazor
- 2026-02-05: Configuración completa de base de datos, API, y frontend
- 2026-02-05: Map page con Leaflet y polígonos de lotes
- 2026-02-06: CRUD completo para Campos (crear, editar, eliminar)
- 2026-02-06: CRUD completo para Lotes con selector de campo y toggle de estado
- 2026-02-06: CRUD completo para Órdenes de Trabajo con workflow de estado
- 2026-02-06: Módulo Inventario con unidad dual (UnitA/UnitB/ConversionFactor)
- 2026-02-06: API InventoryController con CRUD completo
- 2026-02-06: Editor GIS con Leaflet.Draw para dibujo de polígonos
- 2026-02-06: Importación de archivos GeoJSON/KML
- 2026-02-06: Panel de lotes huérfanos (sin geometría)
- 2026-02-06: Comparación de superficies fiscal vs dibujada con delta
- 2026-02-06: Estilos dark theme para modales, formularios, inputs, selects
- 2026-02-06: Menú lateral con sección Logística (Inventario)
- 2026-02-06: Labor y LaborSupply entities con tablas Labors/LaborSupplies
- 2026-02-06: LaborsController API con CRUD, /realize, /replicate endpoints
- 2026-02-06: OrdenesTrabajos master-detail con labor cards y supply editing
- 2026-02-06: Startup DB migration para asegurar columnas y tablas en Supabase
- 2026-02-06: Fix Row/Col Razor syntax (fully qualified AntDesign.Row/Col)
- 2026-02-06: Fix Inventory nullable UnitA/UnitB handling
- 2026-02-06: Dashboard Inmersivo con mapa satelital de fondo (Leaflet sin controles)
- 2026-02-06: Overlay gradiente semitransparente y widgets KPI glassmorphism flotantes
- 2026-02-06: Sidebar glassmorphic con branding AGRIVANT y layout transparente
- 2026-02-06: JS initDashboardMap/addDashboardLotPolygon/fitDashboardLots para mapa de fondo
- 2026-02-09: Rebranding a "Gestor OT" con logo de empresa GestorMax en sidebar
- 2026-02-11: Refactor tarifas: Rate/RateUnit movido de WorkOrder a Labor (cada labor tiene su propia tarifa)
- 2026-02-11: ServiceSettlement calcula TotalAmount = Sum(Labor.Rate × EffectiveArea) con desglose por labor
- 2026-02-11: LaborSettlementLineDto para líneas de detalle en liquidación
- 2026-02-11: UI: Tarifa/unidad en formulario y cards de labor, tabla desglose en liquidación
- 2026-02-11: Estrategias: Eliminado campo AgreedRate del formulario de aplicación
- 2026-02-11: DB migration: Columnas Rate/RateUnit agregadas a tabla Labors
- 2026-02-11: Multi-tenancy inicial con EF Core Global Query Filters (reemplazado por RLS)
- 2026-02-11: Magic Links con SHA256 token hashing para acceso anónimo de contratistas
- 2026-02-11: Panel Admin con RBAC: gestión de usuarios, roles, perfiles
- 2026-02-11: Tank Mix Rules: reglas de incompatibilidad entre productos agroquímicos
- 2026-02-11: AuditLog con SaveChangesInterceptor para WorkOrder/Labor changes
- 2026-02-11: AgronomicValidationService: validación de mezclas con alertas Danger/Warning
- 2026-02-11: Admin pages (Users, TankMix, Audit) con AntDesign Table
- 2026-02-11: Tank Mix validation integrada en flujo de creación de labores
- 2026-02-12: Admin Panel V2: QuickGrid + Paginator para Users, AdminDashboardLayout con sidebar colapsable
- 2026-02-12: Import isolation: AntDesign per-page imports, Admin/_Imports.razor sin AntDesign para evitar conflictos
- 2026-02-12: PersistentComponentState para persistencia de búsqueda/filtro en Admin/Users
- 2026-02-12: AddValidation() .NET 10, UserFormDto/ProductFormDto con DataAnnotations
- 2026-02-12: ITenantService + MockTenantService con 5 tenants de ejemplo
- 2026-02-12: NotFound.razor (404) con Router NotFoundPage property
- 2026-02-12: Consolidación: AdminUsers/AdminTankMix/AdminAudit movidos a Pages/Admin/
- 2026-02-12: RLS multi-tenancy: PostgreSQL Row-Level Security reemplaza EF Core Query Filters
- 2026-02-12: TenantSessionInterceptor inyecta SET app.current_tenant en cada conexión DB
- 2026-02-12: Eliminados todos los HasQueryFilter del ApplicationDbContext (11 filtros)
- 2026-02-12: TenantProvider.razor con [PersistentState] para persistir TenantId SSR→WASM
- 2026-02-12: Campaign module: entities, CampaignContextService, EF Global Query Filter en WorkOrder
- 2026-02-12: CampaignLockedInterceptor bloquea modificaciones en campañas bloqueadas
- 2026-02-12: CampaignsController API CRUD completo con campos asociados y status workflow
- 2026-02-12: CampaignSelector con [PersistentState] y CampaignHttpHandler para header X-Campaign-ID
- 2026-02-12: Campanias.razor: página completa de gestión de campañas con tabla, CRUD, status workflow
- 2026-02-12: Gestión de campos por campaña: sub-modal para agregar/quitar campos con hectáreas y rendimiento
- 2026-02-12: Link "Campañas" agregado al menú lateral en sección Principal
- 2026-02-12: Work Planner module: data model extensions (OTNumber, PlannedDate, ExpirationDate, EstimatedCostUSD, StockReserved, ContractorId)
- 2026-02-12: LaborSupply: TankMixOrder (secuencia de carga) e IsSubstitute (producto sustituto)
- 2026-02-12: Labor: PrescriptionMapUrl, MachineryUsedId, WeatherLogJson, EvidencePhotosJson
- 2026-02-12: WorkPlanner.razor: calendario CSS Grid con vistas mes/semana y cards color-coded por status
- 2026-02-12: StockValidatorService: validación de stock con soft-commit y reporte de faltantes
- 2026-02-12: IsoXmlExporterService: exportación ISO 11783 TaskData.xml en ZIP
- 2026-02-12: API endpoints: validate-stock, reserve-stock, export-isoxml en WorkOrdersController
- 2026-02-12: Link "Work Planner" agregado al menú lateral en sección Principal
- 2026-02-18: Loose Labors: Labor.WorkOrderId nullable (Guid?) para labores sin OT
- 2026-02-18: Loose Labors API: GET /unassigned, GET /unassigned/count, PATCH /assign-bulk, PATCH /{id}/unassign
- 2026-02-18: Labor entity: Notes field y MetadataExterna JSONB column
- 2026-02-18: FK constraint cambiado a ON DELETE SET NULL (orphan labors on WorkOrder delete)
- 2026-02-18: LaboresSueltas.razor: página completa con pool de labores sin asignar
- 2026-02-18: QuickLaborCreator integrado en LaboresSueltas (modal de creación rápida)
- 2026-02-18: Batch assign: selección múltiple → asignar a OT existente o crear nueva OT
- 2026-02-18: Sidebar: badge con contador de labores sin asignar en menú "Labores Sueltas"
