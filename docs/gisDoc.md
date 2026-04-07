Acá te detallo exactamente cómo está resuelto el mapa del dashboard:

Stack GIS utilizado
Librerías (cargadas vía CDN)
LibreríaVersiónRolLeaflet.js1.9.4Motor de mapas interactivosLeaflet.Draw1.0.4Dibujo/edición de polígonos (usado en el editor de lotes)
Capas de tiles (mapas base)
ProveedorUsoESRI World Imagery (ArcGIS)Satélite — capa por defecto del dashboardESRI World Boundaries & PlacesOverlay de localidades y límites políticosOpenStreetMapCalles y rutas (alternativa)OpenTopoMapTopográfico (alternativa)
Back-end GIS
TecnologíaRolPostGISAlmacena geometrías de lotes como polígonos (SRID 4326)NetTopologySuiteLibrería .NET para procesar geometrías en el servidorST_Area(geography)/10000Calcula hectáreas reales con precisión geodésica

Cómo funciona el mapa de fondo del dashboard
El efecto "mapa inmersivo de fondo" está construido en 3 capas:
┌─────────────────────────────────────────────┐
│ Widgets glassmórficos (z-index alto) │ ← KPIs, stats, órdenes recientes
├─────────────────────────────────────────────┤
│ Overlay gradiente semitransparente │ ← rgba oscuro que suaviza el mapa
├─────────────────────────────────────────────┤
│ <div id="dashboardMap"> — Leaflet full-bg │ ← posición: absolute; inset: 0
└─────────────────────────────────────────────┘Flujo de carga al entrar al dashboard:

HTML: el <div id="dashboardMap"> está posicionado como fondo absoluto cubriendo toda la pantalla
OnAfterRenderAsync (Blazor): tras el primer render, espera 200ms y llama a mapInterop.initDashboardMap vía JS Interop, que inicializa Leaflet con satélite ESRI
API call: hace un GET /api/lots/geojson que el servidor construye leyendo las geometrías PostGIS de todos los lotes del tenant/campaña activa
Dibuja los lotes: por cada feature GeoJSON invierte las coordenadas (GeoJSON es [lng, lat], Leaflet espera [lat, lng]) y dibuja los polígonos con color según estado:
:large_green_circle: Verde (#2ECC71) → lote Activo

:red_circle: Rojo (#E74C3C) → otro estado
Auto-zoom: fitDashboardLots ajusta la vista para encuadrar todos los lotes visibles