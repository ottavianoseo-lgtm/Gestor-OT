# Documentación del Módulo GIS — GestorOT

**GestorMax · Gestor OT** | Actualizado: Abril 2026

---

## Índice

1. [Arquitectura general](#1-arquitectura-general)
2. [Flujo 1: Crear lote → Dibujar geometría → Asignar](#2-flujo-1-crear-lote--dibujar-geometría--asignar)
3. [Flujo 2: Importar geometría desde archivo externo](#3-flujo-2-importar-geometría-desde-archivo-externo)
4. [Flujo 3: Editar geometría existente](#4-flujo-3-editar-geometría-existente)
5. [Interacción Blazor ↔ Leaflet (JS Interop)](#5-interacción-blazor--leaflet-js-interop)
6. [Convenciones WKT y almacenamiento en Postgres](#6-convenciones-wkt-y-almacenamiento-en-postgres)

---

## 1. Arquitectura general

```
┌─────────────────────────────────────────────────────────┐
│  Blazor WASM (GestorOT.Client)                          │
│  ┌──────────────────┐    ┌────────────────────────────┐ │
│  │  Mapa.razor      │◄──►│  mapInterop.js (Leaflet)   │ │
│  │  (C# / Razor)    │    │  L.Control.Draw            │ │
│  └────────┬─────────┘    └────────────────────────────┘ │
│           │ HttpClient                                   │
└───────────┼─────────────────────────────────────────────┘
            ▼
┌─────────────────────────────────────────────────────────┐
│  ASP.NET Core API (GestorOT.Api)                        │
│  PUT /api/lots/{id}  →  LotsController                  │
│  POST /api/lots      →  LotsController                  │
└───────────┬─────────────────────────────────────────────┘
            ▼
┌─────────────────────────────────────────────────────────┐
│  PostgreSQL + PostGIS                                    │
│  Tabla: Lots                                            │
│  Columna: "Geometry" (geometry(Polygon, 4326))          │
│  Columna: "CadastralArea" (decimal)                     │
└─────────────────────────────────────────────────────────┘
```

**Componentes clave:**

| Archivo | Responsabilidad |
|---|---|
| `src/GestorOT.Client/Pages/Mapa.razor` | Página principal GIS (C#/Razor) |
| `src/GestorOT.Api/wwwroot/js/mapInterop.js` | Puente JS con Leaflet + Draw |
| `src/GestorOT.Api/Controllers/LotsController.cs` | CRUD de lotes + geometría |
| `src/GestorOT.Infrastructure/Services/LotQueryService.cs` | Queries con PostGIS |

---

## 2. Flujo 1: Crear lote → Dibujar geometría → Asignar

```
Usuario                   Mapa.razor              mapInterop.js           API
   │                          │                        │                    │
   │── Navega a /lots ─────►  │                        │                    │
   │   (Sin geometría)         │                        │                    │
   │                          │                        │                    │
   │── Clic "Configurar" ───► │  Nav.NavigateTo        │                    │
   │   (link con ?lotId=X)    │  /mapa?lotId=X         │                    │
   │                          │                        │                    │
   │                          │── OnAfterRenderAsync ─►│                    │
   │                          │   InitializeMapAsync   │── initMap() ──────►│
   │                          │                        │   (Leaflet init)   │
   │                          │                        │                    │
   │                          │ lotId presente →       │                    │
   │                          │ _linkingLotId = lotId  │                    │
   │                          │ _showOrphanPanel = true│                    │
   │                          │                        │                    │
   │── Dibuja polígono ──────►│                        │                    │
   │   (Leaflet Draw)         │                        │◄── CREATED event ──│
   │                          │◄── OnPolygonDrawn()────│    (WKT, area)     │
   │                          │   _drawnWkt = wkt      │                    │
   │                          │   _drawnArea = area    │                    │
   │                          │   _selectedLot = null  │                    │
   │                          │                        │                    │
   │── Clic "Guardar         ►│                        │                    │
   │    Geometría"            │── getCurrentBounds() ─►│                    │
   │                          │◄── {SW, NE bounds} ────│                    │
   │                          │                        │                    │
   │                          │── PUT /api/lots/{id} ──────────────────────►│
   │                          │   Body: LotDto con     │                    │
   │                          │   WktGeometry + area   │                    │
   │                          │                        │                ◄───│
   │                          │── ReloadMap(bounds) ──►│                    │
   │                          │                        │── restoreBounds() ►│
   │                          │                        │   (viewport OK)    │
   │◄── Mapa actualizado ─────│                        │                    │
```

**Puntos clave:**
- El `lotId` se pasa como query param desde `Lotes.razor` al navegar a `/mapa?lotId=X`.
- `InitializeMapAsync` detecta el `lotId` y preselecciona el lote en el panel huérfano.
- Antes de `ReloadMap`, se captura el viewport con `getCurrentBounds()` para evitar zoom-out (fix #7).

---

## 3. Flujo 2: Importar geometría desde archivo externo

```
Usuario                   Mapa.razor              mapInterop.js           API
   │                          │                        │                    │
   │── Clic "Importar" ──────►│                        │                    │
   │   (_showImportModal)     │                        │                    │
   │                          │                        │                    │
   │── Selecciona archivo ───►│                        │                    │
   │   (.geojson / .kml)      │── HandleFileImport()   │                    │
   │                          │                        │                    │
   │                          │ Si .kml:               │                    │
   │                          │── parseKmlFile(str) ──►│                    │
   │                          │ Si .geojson:           │                    │
   │                          │── parseGeoJsonFile() ─►│                    │
   │                          │◄── [{wkt, name}, ...]──│                    │
   │                          │                        │                    │
   │                          │── addImportedPolygon() ►│                   │
   │                          │   (por cada polígono)  │── L.polygon() ────►│
   │                          │                        │   (color violeta)  │
   │◄── Polígonos en mapa ────│                        │                    │
   │                          │                        │                    │
   │── Clic "Vincular" ──────►│                        │                    │
   │   (en resultado)         │ _importLinkWkt = wkt   │                    │
   │                          │ _showLinkModal = true  │                    │
   │                          │                        │                    │
   │── Selecciona lote ──────►│                        │                    │
   │   (en modal vincular)    │ _linkingLotId = id     │                    │
   │                          │                        │                    │
   │── Clic "Vincular y      ►│── getCurrentBounds() ─►│                    │
   │    Guardar"              │◄── {bounds} ───────────│                    │
   │                          │── PUT /api/lots/{id} ──────────────────────►│
   │                          │◄── 204 No Content ─────────────────────────│
   │                          │── ReloadMap(bounds) ──►│                    │
   │◄── Mapa actualizado ─────│                        │                    │
```

**Formatos soportados:**
- `.geojson` / `.json`: Parsed con `parseGeoJsonFile()`. Soporta `FeatureCollection`, `Feature` individual y `Polygon` directo.
- `.kml`: Parsed con `parseKmlFile()` via `DOMParser`. Extrae `Placemark` con `coordinates`.

**Límite de archivo:** 10 MB (`maxAllowedSize: 10 * 1024 * 1024`).

---

## 4. Flujo 3: Editar geometría existente

```
Usuario                   Mapa.razor              mapInterop.js           API
   │                          │                        │                    │
   │── Clic en polígono ─────►│                        │                    │
   │   (en mapa)              │◄── OnLotSelected(id) ──│◄── click event ───│
   │                          │   _selectedLot = lot   │                    │
   │                          │   (rail lateral abre)  │                    │
   │                          │                        │                    │
   │── Clic "Editar" (Draw) ─►│                        │── enableEditing() ►│
   │   (botón Leaflet Draw)   │                        │   (edit mode)     │
   │                          │                        │                    │
   │── Modifica polígono ────►│                        │                    │
   │   (drag vértices)        │                        │◄── EDITED event ───│
   │                          │◄── OnPolygonEdited()───│   (wkt, area)     │
   │                          │   _drawnWkt = newWkt   │                    │
   │                          │                        │                    │
   │── Clic "Guardar" ──────►│── getCurrentBounds() ─►│                    │
   │   (Leaflet Draw Save)    │◄── bounds ─────────────│                    │
   │                          │── PUT /api/lots/{id} ──────────────────────►│
   │                          │   Body: LotDto {       │                    │
   │                          │     WktGeometry: wkt,  │                    │
   │                          │     CadastralArea: X } │                    │
   │                          │◄── 204 ────────────────────────────────────│
   │                          │── ReloadMap(bounds) ──►│                    │
   │◄── Geometría actualizada ─│                        │                    │
```

**Nota sobre CadastralArea en edición:**
Si el usuario no modificó el área catastral manualmente, se preserva la existente (`lot.CadastralArea`). El área GIS calculada (`areaHa`) solo se usa como fallback si `CadastralArea == 0` (fix Sprint 1, bug #3/#4).

---

## 5. Interacción Blazor ↔ Leaflet (JS Interop)

### Funciones JS invocadas desde Blazor (`JS.InvokeAsync/VoidAsync`)

| Función JS | Descripción | Parámetros |
|---|---|---|
| `mapInterop.initMap` | Inicializa el mapa Leaflet | `containerId, lat, lng, zoom` |
| `mapInterop.setDotNetRef` | Registra referencia .NET para callbacks | `dotNetRef` |
| `mapInterop.addLotPolygon` | Dibuja un lote en el mapa | `lotId, lotName, status, area, fieldName, coordsJson` |
| `mapInterop.centerOnLot` | Centra y hace zoom al lote | `lotId` |
| `mapInterop.fitAllLots` | Ajusta el viewport a todos los lotes | — |
| `mapInterop.clearLots` | Elimina todos los polígonos del mapa | — |
| `mapInterop.clearDrawn` | Limpia la capa de dibujo | — |
| `mapInterop.enableDrawing` | Activa modo dibujo para un lote | — |
| `mapInterop.getCurrentBounds` | Devuelve el viewport actual | — → `{southWestLat, southWestLng, northEastLat, northEastLng}` |
| `mapInterop.restoreBounds` | Restaura un viewport guardado | `bounds` |
| `mapInterop.searchCity` | Vuela a una ciudad por nombre | `query` |
| `mapInterop.parseGeoJsonFile` | Parsea GeoJSON a lista de WKT | `geoJsonString` → `[{wkt, name}]` |
| `mapInterop.parseKmlFile` | Parsea KML a lista de WKT | `kmlString` → `[{wkt, name}]` |
| `mapInterop.addImportedPolygon` | Muestra polígono importado | `wkt, name` |

### Funciones .NET invocadas desde JS (`[JSInvokable]`)

| Método .NET | Cuándo se invoca | Datos recibidos |
|---|---|---|
| `OnPolygonDrawn(wkt, area)` | Usuario termina de dibujar | WKT string, área en ha |
| `OnPolygonEdited(wkt, area)` | Usuario edita un polígono existente | WKT string, área en ha |
| `OnPolygonDeleted()` | Usuario elimina el polígono dibujado | — |
| `OnLotSelected(lotId)` | Usuario hace clic en un polígono del mapa | GUID del lote |

---

## 6. Convenciones WKT y almacenamiento en Postgres

### Formato WKT usado en el sistema

```
POLYGON ((lng1 lat1, lng2 lat2, ..., lng1 lat1))
```

**Orden de coordenadas:** `longitud latitud` (X Y) — estándar WKT/GeoJSON.  
**Precisión:** 8 decimales (generado por Leaflet en `layerToWkt()`).  
**SRID:** 4326 (WGS84) — asignado explícitamente en `LotsController`:
```csharp
geometry.SRID = 4326;
```

### Conversión Leaflet → WKT

Leaflet trabaja internamente en `[lat, lng]` (Y X). La función `layerToWkt()` invierte el orden:
```js
var coords = latlngs.map(ll => ll.lng.toFixed(8) + ' ' + ll.lat.toFixed(8));
```

La función `ConvertToLeafletCoords()` en Blazor hace el proceso inverso al leer desde GeoJSON:
```csharp
ring.Select(coord => new double[] { coord[1], coord[0] }).ToArray()
// GeoJSON: [lng, lat] → Leaflet: [lat, lng]
```

### Almacenamiento en PostgreSQL

```sql
-- Columna en tabla Lots
"Geometry" geometry(Polygon, 4326)

-- Inserción desde WKT
ST_GeomFromText('POLYGON ((...))', 4326)

-- Cálculo de área catastral (en hectáreas)
ST_Area("Geometry"::geography) / 10000.0
```

**Función en `LotQueryService.CalculateAreaFromWktAsync`:**
```sql
SELECT COALESCE(ST_Area(ST_GeomFromText({wkt}, 4326)::geography) / 10000.0, 0) AS "Value"
```
El cast `::geography` usa el modelo esférico para cálculo preciso de área real (no euclidiana).

### Flujo completo WKT

```
Leaflet Draw
    │  [lat, lng] (modo interno Leaflet)
    ▼
layerToWkt()
    │  "POLYGON ((lng lat, ...))"
    ▼
Blazor OnPolygonDrawn(wkt, area)
    │  _drawnWkt = "POLYGON ((lng lat, ...))"
    ▼
PUT /api/lots/{id}  Body: { wktGeometry: "POLYGON ((...)" }
    │
    ▼
LotsController.UpdateLot()
    │  WKTReader.Read(dto.WktGeometry)
    │  geometry.SRID = 4326
    ▼
Postgres  geometry(Polygon, 4326)
    │  PostGIS calcula área real
    ▼
LotQueryService.GetAllAsync()
    │  WKTWriter.Write(l.Geometry) → "POLYGON ((lng lat, ...))"
    ▼
LotDto.WktGeometry
    │
    ▼
mapInterop.addLotPolygon()
    │  ConvertToLeafletCoords([lng, lat] → [lat, lng])
    ▼
Leaflet L.polygon([[lat, lng], ...])
```

---

*Documento mantenido por el equipo GestorMax. Última revisión: Sprint 4.*
