# 🗺️ ESPECIFICACIONES TÉCNICAS GIS (MAPAS)
Manejo de miles de polígonos agrícolas sin lag.

## 1. INTEROPERABILIDAD "ZERO-COPY" (CRÍTICO)
- [cite_start]**Prohibido:** No usar `IJSRuntime.InvokeAsync` para mover FeatureCollections masivas.
- [cite_start]**Obligatorio:** Usar `[JSImport]` y `[JSExport]` con `MemoryView` para permitir que JavaScript acceda directamente a los datos en la memoria de WASM sin realizar copias[cite: 4, 11].

## 2. RENDIMIENTO DE LEAFLET
- [cite_start]**Canvas Rendering:** Inicializar el mapa con `preferCanvas: true` para renderizar +1000 polígonos fluídamente[cite: 20, 953].
- [cite_start]**Spatial Indexing:** Todas las consultas espaciales en Supabase deben usar índices `GiST` y la función `ST_Intersects` de PostGIS[cite: 5, 4264].

## 3. PIPELINE DE DATOS ESPACIALES
- [cite_start]**Serialización:** Usar `NetTopologySuite.IO.GeoJSON4STJ` integrado con System.Text.Json Source Generators[cite: 13, 15].
- [cite_start]**DTO Safety:** Para evitar crashes de serialización, enviar la geometría desde el Server como `string WKT` o `string GeoJSON` y deserializar en el Client[cite: 13].