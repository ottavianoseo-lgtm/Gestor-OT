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

## Pages
- `/` - Dashboard (KPIs, recent orders)
- `/fields` - Campos CRUD
- `/lots` - Lotes CRUD
- `/workorders` - Órdenes de Trabajo CRUD con workflow de estado
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
