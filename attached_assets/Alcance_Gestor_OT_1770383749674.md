# Alcance y Arquitectura: Gestor OT

**Sistema Satélite de Gestión de Labores Agrícolas & GIS**

| Metadato | Detalle |
| :--- | :--- |
| **Versión** | 2.0.0 (Final) |
| **Fecha** | 30 de Enero, 2026 |
| **Dependencia** | Módulo Satélite de ERP "Gestor Max" |
| **Arquitectura** | Offline-First / Multi-Tenant / Hybrid GIS |

---

## 1. Visión del Producto

**Gestor OT** es la extensión operativa de campo para empresas que utilizan el ERP **Gestor Max**. Su misión es digitalizar el flujo agronómico que ocurre "tranqueras adentro", cubriendo el vacío funcional del ERP en cuanto a geolocalización y ejecución de labores.

### El Desafío "Híbrido"
El ERP Gestor Max es la fuente de la verdad **Administrativa** (Lotes Fiscales, Stock, Personal), pero carece de información **Geográfica** (Coordenadas). **Gestor OT** actúa como el eslabón perdido: consume los datos administrativos y permite al usuario "dibujar" la realidad geográfica sobre ellos, convirtiéndose en la fuente de la verdad **Territorial**.

---

## 2. Módulos Funcionales (Alcance)

### 2.1 Módulo Core & Sync (El Espejo Administrativo)
* **Conexión Unidireccional (GET):** El sistema consume catálogos maestros de Gestor Max (Insumos, Personas, Lotes Fiscales, Cultivos).
* **Multi-Tenancy:** Arquitectura preparada para servir a múltiples empresas (100+) con aislamiento total de datos.
* **Identidad:** Autenticación federada o propietaria vinculada a una empresa del ERP.

### 2.2 Módulo Editor GIS & Onboarding (La Verdad Geográfica)
* **Gestión de Lotes "Huérfanos":** Interfaz para identificar lotes que vienen del ERP pero no tienen ubicación en el mapa.
* **Herramientas de Digitalización:**
    * Dibujo manual de polígonos sobre capa satelital.
    * Importación de archivos `.KML` / `.GEOJSON`.
    * Vinculación manual: "Este polígono que dibujé ES el Lote 'Santa Rosa 1' del ERP".
* **Cálculo de Superficies:** Comparativa automática entre "Hectáreas Fiscales" (ERP) vs "Hectáreas Dibujadas" (GIS).

### 2.3 Módulo de Planificación (Órdenes de Trabajo)
* **Gestión de Campañas:** Definición de ciclos productivos y ajuste de superficie sembrable por año.
* **Wizard de OTs:** Creación de órdenes complejas multi-lote.
* **Motor de Insumos:**
    * Manejo de **Doble Unidad** (Stock en Bidones vs Dosis en Litros).
    * Conversión automática en tiempo real.
* **Estrategias:** Plantillas de labores relativas (Recetas agronómicas) para generación masiva.

### 2.4 Módulo de Ejecución (App de Campo)
* **Offline-First:** Descarga de OTs al dispositivo. Operación completa sin red.
* **Registro de Realidad:**
    * Reporte de **Discrepancias**: "Planifiqué 100L, usé 110L".
    * Adición de insumos extra no planificados.
    * Geolocalización del punto de inicio y fin de la labor.

---

## 3. Stack Tecnológico

* **Backend:** .NET 10 (ASP.NET Core Web API).
* **Frontend:** Blazor WebAssembly (WASM) Hosted.
* **Base de Datos:** PostgreSQL 16 + **PostGIS** (Manejo espacial avanzado).
* **ORM:** Entity Framework Core.
* **Map Engine:** Leaflet.js (Cliente) + Mapbox Satellite (Tiles).
* **UI Library:** Ant Design Blazor (Personalizado).

---

## 4. Registro de Decisiones de Arquitectura (ADR)

### ADR-001: Modelo de Datos Híbrido (GIS Local)
* **Contexto:** El ERP no tiene coordenadas.
* **Decisión:** La entidad `ErpLot` en nuestra DB tiene campos mixtos. `Name` y `FiscalId` se sincronizan (sobrescriben) desde el ERP. `GeoJson` y `CalculatedArea` son propiedad exclusiva de Gestor OT y nunca son tocados por el proceso de Sync.

### ADR-002: Arquitectura Offline-First
* **Decisión:** El cliente Blazor mantiene una copia completa de los datos operativos en `IndexedDB`. La sincronización de subida (Ejecución de OTs) utiliza un patrón de "Outbox Pattern" (Cola de salida) que reintenta hasta tener conexión.

### ADR-003: Multi-Tenancy por Columna
* **Decisión:** Single Database. Todas las tablas implementan `ITenantEntity` (columna `TenantId`). Se usan Global Query Filters de EF Core para seguridad.

### ADR-004: Insumos con Unidad Dual
* **Decisión:** Almacenar `UnitA` (Stock) y `UnitB` (Aplicación) con un `ConversionFactor`. La UI permite al ingeniero elegir la unidad de entrada, pero el sistema normaliza a `UnitB` para cálculos de dosis por hectárea.