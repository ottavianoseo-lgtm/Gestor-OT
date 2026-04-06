# SUPABASE / POSTGRESQL SCHEMA SPECIFICATIONS

## 1. EXTENSIONES
- **Obligatorio:** `CREATE EXTENSION IF NOT EXISTS postgis;`

## 2. TABLAS NATIVAS
- **Fields:** `Id (PK), Name (text), TotalArea (double), CreatedAt`.
- **Lots:** `Id (PK), FieldId (FK), Name (text), Status (text), Geometry (geometry(Polygon, 4326))`.
- **WorkOrders:** `Id (PK), LotId (FK), Description (text), Status (text), AssignedTo (text), DueDate (timestamp)`.
- **Inventory:** `Id (PK), Category (text), ItemName (text), CurrentStock (double), ReorderLevel (double)`.

## 3. RELACIONES
- Un Lote pertenece a un Campo.
- Una Orden de Trabajo está vinculada a un Lote.
