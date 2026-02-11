# ⚙️ LÓGICA DE NEGOCIO MAESTRA: GESTIÓN DE OTs, LABORES Y ESTRATEGIAS

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
