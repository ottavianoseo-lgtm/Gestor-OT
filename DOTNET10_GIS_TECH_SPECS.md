# 🗺️ GIS & PERFORMANCE TECHNICAL SPECIFICATIONS (.NET 10)

## 1. INTEROPERABILIDAD DE ALTO RENDIMIENTO (JSImport)
- **Patrón:** Abandonar `IJSRuntime` para la transferencia de GeoJSON. 
- **Implementación:** Usar `[JSImport]` y `[JSExport]` de .NET 10.
- **Memoria:** Utilizar `MemoryView` para pasar el buffer de coordenadas directamente desde el heap de WASM a JavaScript sin copias intermedias (Zero-copy).
- **JS Integration:** El archivo `map.js` debe exponer funciones tipadas compatibles con `JSImport`.

## 2. SERIALIZACIÓN OPTIMIZADA (Source Generation)
- **Regla:** Prohibido el uso de serialización basada en reflexión.
- **Implementación:** Definir un `JsonSerializerContext` parcial. 
- **Configuración:** Registrar todos los DTOs que incluyan tipos `Geometry` o `FeatureCollection`. Esto es crítico para mantener el rendimiento bajo AOT (Ahead-Of-Time compilation).

## 3. POSTGIS & EF CORE 10
- **Conexión:** `Npgsql.EntityFrameworkCore.PostgreSQL.NetTopologySuite` es obligatorio.
- **Mapeo:** Las columnas espaciales deben definirse como `geometry(Polygon, 4326)`.
- **Bulk Operations:** Usar `context.Lots.ExecuteUpdateAsync(...)` para cambios de estado masivos (ej. cambiar status de 50 lotes a la vez) para evitar el overhead del Change Tracker de EF.
- **Índices:** Las migraciones deben incluir índices `GiST` en las columnas de geometría para optimizar consultas de intersección (`ST_Intersects`).

## 4. RENDIMIENTO DEL MAPA (Leaflet)
- **Renderer:** Forzar `preferCanvas: true` en las opciones del mapa. Esto permite manejar +1000 polígonos agrícolas sin lag en el navegador.
- **Clustering:** Implementar clustering de puntos si la densidad de Work Orders en el mapa supera los 100 elementos por vista.
