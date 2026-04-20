    •	Sprint 3 (GIS): Bloque 2 completo (2.1 buscador ciudad, 2.2 crear lote desde GIS, 2.4 variación superficie, 2.5 historial).
BLOQUE 2  Módulo GIS y Gestión de Lotes

2.1 · Buscador de Ciudad en la Pantalla GIS
Estado actual: Mapa.razor no tiene input de búsqueda. El mapa usa Leaflet via mapInterop.js.

Archivos a Modificar / Crear
Archivo / Componente	Acción
Mapa.razor	Agregar componente de búsqueda en top-center
mapInterop.js	Agregar función searchCity(query) usando geocoder
GestorOT.Api/wwwroot/css/main-layout.css	Estilos para el buscador GIS

Plan de Acción Frontend
•	En Mapa.razor, agregar un <div class="gis-search"> posicionado top: 12px; left: 50%; transform: translateX(-50%); z-index: 1000 que contenga un <Input> de AntDesign con placeholder="Buscar ciudad..." y un ícono de lupa.
•	Al presionar Enter o el botón de búsqueda, invocar JS.InvokeVoidAsync("mapInterop.searchCity", _cityQuery).
•	En mapInterop.js, implementar searchCity(query) usando la API de Nominatim (OpenStreetMap) vía fetch: GET https://nominatim.openstreetmap.org/search?q={query}&format=json&limit=1. Con el resultado, llamar map.flyTo([lat, lng], 12).

Plan de Acción Backend & DB
No se requieren cambios en el backend. La búsqueda es 100% client-side vía Nominatim (API pública, sin API key).
Nota: Si el ambiente es privado/offline, considerar Photon geocoder (self-hosteable) como alternativa a Nominatim.

2.2 · Crear Lote desde GIS cuando no hay huérfanos disponibles
Estado actual: Al dibujar un polígono en Mapa.razor, si _orphanLots está vacío, el dropdown de vinculación queda vacío y el usuario no puede guardar.

Archivos a Modificar / Crear
Archivo / Componente	Acción
Mapa.razor	Agregar flujo de creación de lote inline
LotsController.cs	POST /api/lots ya existe — solo verificar que devuelva el ID
FieldsController.cs	GET /api/fields — para el selector de campo al crear lote

Plan de Acción Frontend
•	En el rail lateral de Mapa.razor (sección "Polígono Dibujado"), detectar si _orphanLots.Count == 0. Si es así, mostrar un Button secundario "Crear nuevo lote" bajo el Select de vinculación.
•	Al hacer clic, abrir un sub-formulario inline (dentro del rail) con dos campos: Select de Campo (Field) y Input de Nombre del lote.
•	Al confirmar, llamar POST /api/lots con { FieldId, Name, Status: "Active" }. El ID del nuevo lote se usa para _linkingLotId y se llama SaveDrawnGeometry().
•	Tras guardar, agregar el lote recién creado a _allLots y limpiar el sub-formulario.

Plan de Acción Backend & DB
•	POST /api/lots ya existe en LotsController.cs y crea el lote correctamente. No se requieren cambios.
•	Verificar que la respuesta incluya el ID generado (CreatedAtAction con el objeto) — ya está implementado.

2.3 · Calcular y Persistir CadastralArea al guardar geometría
Estado actual (Bug): Lot.CadastralArea existe en la entidad pero jamás se calcula automáticamente. En LotsController.cs (PUT /api/lots/{id}), se guarda la geometría pero no se recalcula el área catastral.

Archivos a Modificar / Crear
Archivo / Componente	Acción
LotsController.cs	Modificar PUT para calcular y persistir CadastralArea
LotQueryService.cs	Exponer método GetLotAreaByIdAsync(Guid) para reutilizar lógica SQL
ILotQueryService.cs	Agregar declaración del método

Plan de Acción Backend & DB
•	En LotsController.cs, en el endpoint PUT /api/lots/{id}, después de asignar lot.Geometry y antes de SaveChangesAsync(), agregar el cálculo de área vía SQL espacial:
○	var area = await _context.Database.SqlQueryRaw<double>("SELECT ST_Area($1::geography)/10000.0 AS \"Value\"", wkt).FirstOrDefaultAsync();
○	Alternativamente, usar NetTopologySuite: lot.CadastralArea = (decimal)(lot.Geometry.Area * 10000); pero con proyección geográfica correcta (EPSG:4326 requiere conversión).
•	RECOMENDADO: Usar el mismo patrón SQL que ya existe en LotQueryService.GetLotAreasAsync() — ejecutar la query ST_Area(...::geography)/10000 para el lote recién guardado y asignar el resultado a lot.CadastralArea antes del commit.
•	En POST /api/lots (creación): aplicar la misma lógica si se recibe WktGeometry en la request.
No se requiere migración: el campo CadastralArea ya existe en la entidad Lot y en la tabla correspondiente (confirmado en el snapshot de EF Core).

2.4 · Corrección Bug: Total Catastral en 0 en Pantalla de Lotes por Campaña
Estado actual: CampaignLotEditor.razor o Lotes.razor muestran total catastral = 0 porque CadastralArea nunca fue persistido (fix 2.3 lo resuelve) y además el DTO de CampaignLot no expone CadastralArea.

Archivos a Modificar / Crear
Archivo / Componente	Acción
CampaignDto.cs (Shared)	Agregar CadastralArea en CampaignLotDto
CampaignsController.cs	Incluir lot.CadastralArea en la proyección de GET .../lots
CampaignLotEditor.razor	Mostrar el dato en la UI
Lotes.razor	Mostrar total catastral y variación de superficie

Plan de Acción Frontend
•	En CampaignDto.cs (Shared), agregar la propiedad CadastralArea en el record CampaignLotDto.
•	En Lotes.razor o CampaignLotEditor.razor, agregar columna "Área Catastral" y una columna calculada "Variación" = ProductiveArea - CadastralArea, con color rojo si negativa y verde si positiva.

Plan de Acción Backend & DB
•	En CampaignsController.cs, en el endpoint GET /api/campaigns/{id}/lots, incluir en la proyección: CadastralArea = cl.Lot != null ? cl.Lot.CadastralArea : 0m.
•	Con el fix 2.3, los lotes tendrán CadastralArea correctamente poblado. Ejecutar un UPDATE masivo post-deploy para recalcular el área de lotes históricos con geometría.

2.5 · Historial de Variación de Superficie Productiva por Campaña
Archivos a Modificar / Crear
Archivo / Componente	Acción
CampaignsController.cs	Nuevo endpoint GET /api/lots/{lotId}/surface-history
ILotQueryService.cs	Declarar GetSurfaceHistoryAsync(Guid lotId)
LotQueryService.cs	Implementar consulta de historial cross-campaign
Lotes.razor / CampaignLotEditor.razor	Componente de historial (Tabs o Collapse)

Plan de Acción Backend & DB
•	Crear endpoint GET /api/lots/{lotId}/surface-history que consulte todos los CampaignLot del lote ordenados por Campaign.StartDate, retornando { CampaignName, StartDate, ProductiveArea, CadastralArea, Variation }.
•	No requiere migración; los datos ya existen en CampaignLot.

Plan de Acción Frontend
•	En la pantalla de detalle de lote, agregar un componente Collapse o un Chart de barras (Ant Design Charts o una tabla simple) que muestre el historial de superficie productiva vs catastral por campaña.