using GestorOT.Shared.Dtos;

namespace GestorOT.Application.Services;

public interface IShapefileImportService
{
    /// <summary>
    /// Lee un shapefile entregado como .zip (los .shp/.shx/.dbf/.prj que exportan QGIS, ArcGIS,
    /// John Deere y compañía) y devuelve los polígonos ya reproyectados a EPSG:4326, que es lo
    /// que guarda y dibuja la app.
    /// </summary>
    Task<ShapefileImportResultDto> ReadZipAsync(Stream zipStream, CancellationToken ct = default);
}
