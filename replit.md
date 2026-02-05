# GestorOT V2 - Sistema de Gestión de Órdenes de Trabajo

## Overview
GestorOT es un sistema de gestión de órdenes de trabajo agrícola con capacidades GIS (PostGIS) para la visualización y gestión de lotes agrícolas. Construido con .NET 10, Blazor WebAssembly, y AntDesign.

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

## Database Schema
- **Fields** - Campos agrícolas (Id, Name, TotalArea, CreatedAt)
- **Lots** - Lotes con geometría PostGIS (Id, FieldId, Name, Status, Geometry)
- **WorkOrders** - Órdenes de trabajo (Id, LotId, Description, Status, AssignedTo, DueDate)
- **Inventory** - Inventario (Id, Category, ItemName, CurrentStock, ReorderLevel)

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
- Surface Light: #FFFFFF

## Recent Changes
- 2026-02-05: Inicialización del proyecto con arquitectura Hosted Blazor
- 2026-02-05: Configuración de Npgsql 10 con NetTopologySuite
- 2026-02-05: Implementación de layout AntDesign con Design System Agrivant
- 2026-02-05: Creación de ApplicationDbContext con PostGIS y entidades
- 2026-02-05: Configuración de AppJsonSerializerContext para Source Generation
