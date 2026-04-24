# LÓGICA DE NEGOCIO MAESTRA: GESTIÓN DE OTs, LABORES Y ESTRATEGIAS

## 1. MODELO DE DOMINIO (ENTIDADES EF CORE 10)

### 1.1 El Núcleo Operativo
* **WorkOrder (Cabecera):**
    * `Id`, `CampaignId` (Contexto), `ContractorId` (Ejecutor), `Date`, `Status` (Draft, Pending, InProgress, Done, Approved, Cancelled).
    * `AgreedRate`: Decimal (Precio pactado por Ha para liquidación).
* **Labor (Detalle):**
    * `Id`, `WorkOrderId` (FK), `CampaignLotId` (FK - Lote físico y temporal).
    * `ActivityType`: Enum (Siembra, Pulverización, Cosecha).
    * `LaborStatus`: Enum (Planned, Realized).
    * `ExecutionDate`: DateTime.
    * `EffectiveArea`: Decimal (Hectáreas reales trabajadas, por defecto el área del lote).
* **LaborSupply (Insumos - El "Snapshot"):**
    * `LaborId` (FK), `SupplyId` (FK - Producto del ERP).
    * `PlannedDose`: Decimal (Lo que el Ing. pidió).
    * `RealizedDose`: Decimal (Lo que el contratista usó). **Nullable hasta ejecución.**
    * `TotalUsed`: Computed (`RealizedDose * Labor.EffectiveArea`).

### 1.2 El Motor de Automatización (Estrategias)
* **CropStrategy:** `Name` (ej: "Soja 1ra"), `CropType`.
* **StrategyItem:** `LaborType`, `DayOffset` (Días desde inicio), `DefaultSupplies` (JSONB).
    * *Lógica:* Al aplicar, genera `Labors` futuras calculando fechas: `StartDate + DayOffset`.

### 1.3 El Puente Financiero (Auto-Facturación)
* **ServiceSettlement (Liquidación):**
    * `WorkOrderId` (FK), `TotalAmount` (`Sum(Labor.EffectiveArea) * WorkOrder.AgreedRate`).
    * `GeneratedAt`: DateTime.
    * `ErpSyncStatus`: Para enviar a Gestor Max.

## 2. REGLAS DE NEGOCIO (SERVICIOS)

### R1: Principio de "Snapshot" (Plan vs. Real)
* Cuando una Labor pasa de `Planned` a `Realized`:
    1.  El sistema **copia** `PlannedDose` a `RealizedDose` automáticamente.
    2.  El usuario (Contratista) **confirma** o **edita** la `RealizedDose` si hubo variación.
    3.  Se calcula la **Discrepancia** (%) para reportes de eficiencia.

### R2: Flujo de Aprobación y Liquidación

* **Trigger:** Cambio de estado de OT a `Approved`.
* **Acción:**
    1.  Validar que todas las labores hijas estén en `Realized`.
    2.  Sumar hectáreas totales trabajadas.
    3.  Generar entidad `ServiceSettlement`.
    4.  Emitir alerta: "Proforma de Liquidación generada por $X para [Contratista]".

### R3: Aplicación Masiva (Estrategias)
* **Input:** Selección de 1 Estrategia + 50 Lotes + Fecha Inicio.
* **Proceso:**
    1.  Crear 1 OT por Lote (o 1 OT agrupada, configurable).
    2.  Insertar Labores e Insumos calculados.
    3.  **Performance:** Usar `BulkInsert` de EF Core.

---

## 3. POLÍTICA MULTI-EMPRESA (MULTI-TENANT)

> **Decisión formal — Abril 2026**
> Modelo elegido: **Modelo A — Aislado con excepción de catálogo ERP compartido.**

### 3.1 Definición

GestorOT soporta múltiples empresas (Tenants) en una misma instalación. Cada tenant es completamente independiente: sus datos no son visibles ni modificables por otros tenants, **con una única excepción explícita** para el catálogo de actividades ERP.

### 3.2 Regla general — Datos aislados por Tenant

Toda entidad del dominio lleva un campo `TenantId` (Guid) que identifica al propietario del dato. El `ApplicationDbContext` aplica un **query filter global** en cada entidad:

```csharp
entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
```

Esto garantiza que **ninguna consulta** retorna datos de otro tenant, sin necesidad de filtros manuales en cada endpoint. El `CurrentTenantId` se lee del header HTTP `X-Tenant-ID` en cada request.

**Entidades alcanzadas por este filtro (aislamiento total):**

| Entidad | Descripción |
|---|---|
| `Field` | Campos agrícolas |
| `Lot` | Lotes |
| `Campaign` / `CampaignField` / `CampaignLot` | Campañas y sus relaciones |
| `Rotation` | Rotaciones de cultivo |
| `WorkOrder` | Órdenes de trabajo |
| `Labor` / `LaborSupply` / `LaborAttachment` | Labores e insumos |
| `Contact` | Contactos / responsables |
| `Inventory` | Inventario |
| `CropStrategy` / `StrategyItem` | Estrategias de cultivo |
| `TankMixRule` | Reglas de mezcla de tanques |
| `WorkOrderStatus` | Estados de OT configurados |
| `UserProfile` | Perfiles de usuario |
| `AuditLog` | Logs de auditoría |
| `SharedToken` | Tokens de acceso público |
| `ErpPerson` | Personas sincronizadas desde ERP |
| `ErpConcept` | Conceptos/insumos sincronizados desde ERP |
| `LaborType` | Tipos de labor |

### 3.3 Excepción — ErpActivity: catálogo compartido

`ErpActivity` (actividades/cultivos del ERP) es la **única entidad compartida** entre todos los tenants. Un registro de `ErpActivity` con `TenantId = Guid.Empty` (todos ceros) es visible para cualquier tenant activo.

**Filtro especial aplicado:**

```csharp
// ApplicationDbContext.cs — línea 161
entity.HasQueryFilter(e =>
    e.TenantId == Guid.Empty          // registro global (compartido)
    || CurrentTenantId == Guid.Empty  // sin tenant activo (admin/seed)
    || e.TenantId == CurrentTenantId  // registro propio del tenant
);
```

**Regla de negocio:**
- Los registros con `TenantId = Guid.Empty` son el **catálogo base** sincronizado desde el ERP central. Son de solo lectura para los tenants.
- Cada tenant puede tener además sus propias actividades con `TenantId` propio (registros privados).
- La sincronización ERP (`ErpSyncWorker`) puede escribir registros globales (`TenantId = Guid.Empty`) o específicos por tenant según la configuración de la integración.

### 3.4 Mecanismo de aislamiento en operaciones de escritura (PUT/DELETE)

`FindAsync` de EF Core **no aplica** los query filters globales, por lo que puede retornar registros de cualquier tenant si se conoce el ID. Para prevenir esto, todos los endpoints PUT y DELETE de recursos sensibles usan `FirstOrDefaultAsync` con predicado explícito, que sí respeta los filtros:

```csharp
// Correcto — respeta el query filter de tenant:
var lot = await _context.Lots.FirstOrDefaultAsync(l => l.Id == id);

// Incorrecto — bypasea el query filter:
// var lot = await _context.Lots.FindAsync(id);
```

**Controllers auditados y corregidos (Sprint 4):**
- `LotsController` — PUT y DELETE
- `FieldsController` — PUT y DELETE
- `WorkOrdersController` — PUT, PUT/status y DELETE
- `CampaignsController` — PUT/status, DELETE, POST/fields, DELETE/fields, POST/lots, PUT/lots, DELETE/lots, POST/lots/batch

### 3.5 Qué NO está soportado (fuera de scope)

- **Modelo B (Grupo):** No existe un `GroupId` para compartir datos entre tenants relacionados (ej: empresas del mismo grupo económico). Si se requiere en el futuro, implica agregar `GroupId` a la entidad `Tenant`, una migration de BD, y actualizar los query filters para que acepten tanto `TenantId == currentTenant` como `TenantId pertenece al mismo grupo`.
- **Roles cross-tenant:** Un usuario solo puede operar dentro de un único tenant por sesión. No hay impersonación ni acceso multi-empresa desde un mismo login.

### 3.6 Cómo agregar una nueva entidad siguiendo la política

1. Heredar de `TenantEntity` (o implementar `ITenantEntity`):
   ```csharp
   public class MiEntidad : TenantEntity { ... }
   ```
2. Agregar el query filter en `ApplicationDbContext.OnModelCreating`:
   ```csharp
   modelBuilder.Entity<MiEntidad>(entity => {
       entity.HasQueryFilter(e => CurrentTenantId == Guid.Empty || e.TenantId == CurrentTenantId);
   });
   ```
3. En los endpoints PUT/DELETE, usar `FirstOrDefaultAsync` en lugar de `FindAsync`.
4. Si es un catálogo compartido (como `ErpActivity`), usar el filtro extendido con `e.TenantId == Guid.Empty`.
