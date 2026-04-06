# 🔌 SUPABASE CONNECTION & DATA PIPELINE SPECS

## 1. CONFIGURACIÓN DE CADENA DE CONEXIÓN
- **Ubicación:** `GestorOT.Server/appsettings.json`.
- **Formato Npgsql:** Convertir la URI de Supabase al formato de cadena de conexión estándar de .NET si es necesario para compatibilidad con `NpgsqlDataSourceBuilder`.
- **Pooling:** Habilitar el pooling de conexiones para optimizar el rendimiento en Replit.

## 2. INICIALIZACIÓN DEL ORIGEN DE DATOS (Npgsql 10)
- **Obligatorio:** Utilizar `NpgsqlDataSourceBuilder` en el `Program.cs` del Server antes de registrar el DbContext.
- **Mapeo GIS:** Se DEBE invocar `.UseNetTopologySuite()` en el builder del DataSource para habilitar la traducción de tipos `Geometry`.

## 3. CONFIGURACIÓN DEL DBCONTEXT
- **Provider:** Usar `options.UseNpgsql(dataSource)`.
- **SRID:** Configurar el SRID predeterminado en 4326 (WGS84) para todas las operaciones espaciales.
- **Logging:** En modo desarrollo, habilitar `EnableSensitiveDataLogging` y `LogTo(Console.WriteLine)` para capturar errores de traducción SQL de PostGIS.

## 4. SEGURIDAD DE LA CONEXIÓN
- **SSL Mode:** Configurar `SSL Mode=Require` o `Trust Server Certificate=true` según los requerimientos de Supabase para conexiones externas desde entornos cloud (Replit).
