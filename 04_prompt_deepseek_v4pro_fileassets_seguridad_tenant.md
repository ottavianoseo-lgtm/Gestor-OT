# Prompt para DeepSeek V4Pro — Seguridad, Tenant y Endurecimiento de FileAssets

## Rol

Eres **DeepSeek V4Pro**, especialista senior en **C# .NET 10, ASP.NET Core, EF Core, seguridad multi-tenant y manejo seguro de archivos**.

## Requisito obligatorio de Context7 MCP

Antes de modificar código, debes usar obligatoriamente el **MCP de Context7** para consultar documentación actualizada de:

- EF Core 10 global query filters y tracking
- ASP.NET Core authorization y file endpoints
- Buenas prácticas de descarga segura de archivos
- Blazor/ASP.NET Core multi-tenant patterns si están disponibles

Debes incluir una sección final llamada **“Documentación consultada vía Context7”**.

## Rama y objetivo

Repositorio: `ottavianoseo-lgtm/Gestor-OT`  
Rama de trabajo: `fix/bugs-revision-gestor-ot`

Objetivo: endurecer la seguridad y consistencia tenant del módulo de archivos/adjuntos.

## Archivos a revisar obligatoriamente

- `src/GestorOT.Api/Controllers/FilesController.cs`
- `src/GestorOT.Api/Controllers/LaborAttachmentsController.cs`
- `src/GestorOT.Domain/Entities/FileAsset.cs`
- `src/GestorOT.Domain/Entities/LaborFileAsset.cs`
- `src/GestorOT.Infrastructure/Data/ApplicationDbContext.cs`
- `src/GestorOT.Infrastructure/Data/Configurations/FileAssetConfiguration.cs`
- `src/GestorOT.Infrastructure/Data/Configurations/LaborFileAssetConfiguration.cs`
- `src/GestorOT.Application/Interfaces/IApplicationDbContext.cs`
- Tests en `src/GestorOT.Tests`

## Problema detectado

`FilesController` usa `FindAsync` en operaciones sensibles:

- `Download(Guid id)`
- `Delete(Guid id)`
- `LinkFiles(LinkFilesRequest request)` para buscar `Labor`

En este proyecto ya existe una regla técnica importante: no usar `FindAsync` en recursos tenant-scoped porque puede evitar filtros globales de tenant. Debe usarse `FirstOrDefaultAsync` respetando query filters y validaciones explícitas.

## Implementación requerida

### 1. Reemplazar `FindAsync` en recursos sensibles

- `Download`: buscar el archivo con query que respete tenant.
- `Delete`: buscar el archivo con query que respete tenant y validar que no tenga vínculos.
- `LinkFiles`: buscar labor por query filtrada y validar campaña no bloqueada.

### 2. Validar si `FileAsset` debe ser tenant-scoped

Revisar si `FileAsset` hereda de entidad tenant o implementa `ITenantEntity`.

- Si es tenant-scoped: asegurar `TenantId` y query filter.
- Si es biblioteca global: justificarlo explícitamente y aplicar reglas de visibilidad/roles.
- Por defecto funcional del sistema: los archivos no deben ser visibles por todos los usuarios ni cruzar empresas.

### 3. Validar existencia de archivos antes de vincular

En `LinkFiles`:

- Verificar que todos los `FileAssetIds` existan y sean accesibles al tenant actual.
- Si alguno no existe/no es visible, devolver 400/404 claro.
- Evitar crear vínculos con FK inválida.
- Evitar duplicados.

### 4. Bloquear modificaciones en campaña bloqueada

Validar para:

- Upload con `laborId`
- LinkFiles
- Unlink
- Delete si el archivo está vinculado a labor de campaña bloqueada

### 5. Auditoría mínima

Si ya existe `AuditLog`, registrar acciones críticas:

- Upload
- Link
- Unlink
- Delete

Si no existe patrón claro de auditoría, dejar TODO técnico documentado pero no inventar arquitectura incompatible.

## Criterios de aceptación funcional

- Un tenant no puede descargar archivo de otro tenant.
- Un tenant no puede vincular archivo de otro tenant a una labor propia.
- No se puede vincular archivo inexistente.
- No se puede desvincular archivo de labor en campaña bloqueada.
- No se puede eliminar archivo vinculado.
- No se puede subir archivo a labor de campaña bloqueada.
- Las consultas respetan filtros globales y no usan `FindAsync` en endpoints sensibles.

## Tests obligatorios

Agregar tests para:

1. `Download_ReturnsNotFound_ForFileFromAnotherTenant`.
2. `Delete_ReturnsNotFoundOrForbidden_ForFileFromAnotherTenant`.
3. `LinkFiles_ReturnsBadRequest_WhenFileIdDoesNotExist`.
4. `LinkFiles_DoesNotDuplicateExistingLink`.
5. `Unlink_ReturnsConflict_WhenCampaignLocked`.
6. `Upload_ReturnsConflict_WhenLaborCampaignLocked`.

## Restricciones técnicas

- No exponer nombres/rutas internas del servidor en errores.
- No retornar contenido binario si el usuario/tenant no tiene permiso.
- No usar `IgnoreQueryFilters()` salvo justificación explícita y test que lo cubra.
- Mantener límite de tamaño y validación MIME básica.

## Entregable esperado

- Código corregido.
- Tests agregados.
- Informe con comandos `dotnet build` y `dotnet test`.
- Sección **Documentación consultada vía Context7**.
