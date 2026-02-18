# GestorOT V2 - Sistema de Gestión de Órdenes de Trabajo

## Overview
GestorOT is an agricultural work order management system with GIS capabilities (PostGIS) for visualizing and managing agricultural lots. It provides full CRUD operations for Fields, Lots, Work Orders, and Inventory, a GIS editor with Leaflet.Draw, GeoJSON/KML import, and surface comparison. The project aims to streamline agricultural operations and enhance decision-making through spatial data.

## User Preferences
I want iterative development.
I prefer clear and concise explanations.
Ask before making major architectural changes or adding new external dependencies.
Do not make changes to files related to authentication without explicit approval.

## System Architecture

### Solution Structure
- **GestorOT.Server**: ASP.NET Core backend with EF Core and Supabase/PostgreSQL.
- **GestorOT.Client**: Blazor WebAssembly frontend using AntDesign.
- **GestorOT.Shared**: Contains DTOs, Enums, validations, and JsonSerializerContext for shared models.

### Key Technologies
- **Backend**: .NET 10.0, Entity Framework Core 10 (Npgsql), NetTopologySuite, Supabase/PostgreSQL with PostGIS.
- **Frontend**: Blazor WebAssembly (Interactive) with AntDesign Blazor UI, Leaflet.js + Leaflet.Draw for GIS editing.
- **Performance**: Native AOT compilation for Release builds (RunAOTCompilation, WasmStripILAfterAOT, WasmEnableSIMD), PublishTrimmed + TrimMode=link for size optimization, AppJsonSerializerContext for AOT-compatible JSON serialization.

### UI/UX Design (Agrivant UI)
- **Color Scheme**: Primary Red (#E74C3C), Status Green (#2ECC71), Status Gold (#F1C40F), Surface Dark (#1E1E2E), Background (#0f0f1a).
- **Design Pattern**: Glassmorphism with `backdrop-filter blur(10px)` and `rgba` borders.
- **Dashboard**: Immersive dashboard with background satellite map, semi-transparent gradient overlay, and glassmorphic KPI widgets.
- **Sidebar**: Glassmorphic with AGRIVANT branding and transparent layout.

### Feature Specifications
- **CRUD Operations**: Complete Create, Read, Update, Delete for Fields, Lots, Work Orders, and Inventory.
- **GIS Editor**: Leaflet map with Esri satellite tiles, Leaflet.Draw for polygon creation/editing, GeoJSON/KML import, surface comparison (fiscal vs. drawn area) with delta calculation. Lot polygons are colored by status.
- **Inventory Management**: Dual unit system (UnitA, UnitB, ConversionFactor) with real-time conversion display.
- **Work Order Workflow**: Status progression (Pending → InProgress → Completed) with quick-action buttons and KPI tracking. Supports "Loose Labors" (labors without assigned Work Orders) and batch assignment.
- **Work Planner**: CSS Grid calendar (month/week views) displaying "Labores" (tasks) with color-coded statuses and types. Includes quick labor creation and detailed cards.
- **Multi-tenancy**: Implemented via PostgreSQL Row-Level Security (RLS) and `TenantSessionInterceptor` to inject `app.current_tenant`.
- **Campaigns Module**: Manages campaigns with associated fields, status workflows, and field allocation. Uses `CampaignSelector` and `CampaignHttpHandler`.
- **Admin Panel**: User and role management (RBAC), Tank Mix Rules for agrochemical compatibility, and Audit Log for tracking changes. Uses QuickGrid and Paginator.
- **Agronomic Validation**: `AgronomicValidationService` for validating tank mixes with alerts.
- **ISO XML Export**: `IsoXmlExporterService` for exporting ISO 11783 TaskData.xml in ZIP format.

### Client-Side Patterns
- **Data Loading**: `OnInitializedAsync` with `CancellationToken` timeout.
- **Error Handling**: AntDesign Alert components for error display, `IMessageService` for toast notifications.
- **UI Updates**: Explicit `StateHasChanged()` in `finally` blocks for reliable UI rendering.
- **Modals**: AntDesign Modal + Form components for CRUD operations with client-side validation.
- **JS Interop**: Uses global `window.mapInterop` object for Leaflet integration, invoked via `IJSRuntime.InvokeAsync`.
- **State Management**: `PersistentComponentState` for persisting search/filter criteria.

### Database Schema
- **Fields**: Basic agricultural field information.
- **Lots**: Lots with PostGIS geometry.
- **WorkOrders**: Orders for agricultural tasks, linked to Lots.
- **Inventories**: Inventory items with dual unit tracking.
- **Labors**: Individual tasks, can be linked to WorkOrders or be "loose".
- **Cultivos**: Crop types with variety and cycle information.
- **PlanificacionCultivos**: Lot-Campaign-Crop planning with user-editable sown area (SuperficieSembradaHa) and PostGIS-calculated geometry area (SuperficieGeometriaHa). Unique constraint on (LoteId, CampanaId).

### API Endpoints
- **Fields, Lots, Work Orders, Inventory**: Standard RESTful CRUD endpoints.
- **Dashboard**: `GET /api/dashboard/stats`, `GET /api/dashboard/recent-orders`.
- **Labors**: `GET /api/labors/calendar`, `POST validate-stock`, `POST reserve-stock`, `GET export-isoxml`.
- **Unassigned Labors**: `GET /unassigned`, `GET /unassigned/count`, `PATCH /assign-bulk`, `PATCH /{id}/unassign`.
- **Cultivos**: `GET/POST/PUT/DELETE /api/cultivos` - CRUD for crop types.
- **Planificación Cultivos**: `GET/POST/PUT/DELETE /api/planificacion-cultivos` - Lot-Campaign crop planning with filtering by campanaId/loteId.
- **Superficie Campo**: `GET /api/campos/{campoId}/campanas/{campanaId}/superficie` - Aggregate sown/geometry area for all active lots in a field-campaign.
- **Rotación**: `GET /api/lotes/{loteId}/rotacion` - Crop rotation history for a lot ordered by campaign start date.

## External Dependencies

- **Database**: Supabase/PostgreSQL with PostGIS extension.
- **UI Framework**: AntDesign Blazor.
- **GIS Mapping**: Leaflet.js and Leaflet.Draw (loaded via CDN).
- **GIS Geometry**: NetTopologySuite.
- **.NET Data Access**: Entity Framework Core.
- **Identity**: Magic Links with SHA256 token hashing for anonymous access.