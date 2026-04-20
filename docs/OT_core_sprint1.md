3.3 · Bug: Insumos Planeados en Detalle de OT — Solo muestra el último labor
Estado actual: En WorkOrderDetail.razor TabPane "Planeado (Insumos)", cuando la OT tiene más de 2 labores con insumos, solo se renderiza el insumo del último labor. El bug está en WorkOrderQueryService.cs en la proyección de SupplyApprovals, que parece consolidar incorrectamente.

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

5.1 · Eliminar tipografías en color negro — Dark Mode
Estado actual: main-layout.css ya tiene overrides globales para Ant Design (tablas, modales, forms, etc.) usando color: rgba(255,255,255,0.85). Sin embargo, algunos componentes de Ant Design inyectan estilos inline o clases específicas que no son cubiertas por los selectores actuales.