# Prompt para DeepSeek V4Pro — Adjuntos, Biblioteca y Cancelación de Labor Nueva

## Rol

Eres **DeepSeek V4Pro**, especialista senior en **C# .NET 10, ASP.NET Core, EF Core, Blazor y UX de formularios transaccionales**.

## Requisito obligatorio de Context7 MCP

Antes de modificar código, debes usar obligatoriamente el **MCP de Context7** para consultar documentación actualizada de:

- Blazor `InputFile`, streams y límites de tamaño
- ASP.NET Core file upload con `IFormFile`
- EF Core 10 para relaciones many-to-many explícitas
- AntDesign Blazor `Modal`, `Popconfirm`, `Upload`/`Button`, `MessageService`

Debes incluir una sección final llamada **“Documentación consultada vía Context7”**.

## Rama y objetivo

Repositorio: `ottavianoseo-lgtm/Gestor-OT`  
Rama de trabajo: `fix/bugs-revision-gestor-ot`

Objetivo: dejar completo el flujo de adjuntos de labores, incluyendo carga antes de guardar, biblioteca reutilizable, selección desde labor existente y comportamiento correcto al cancelar una labor nueva.

## Archivos a revisar obligatoriamente

- `src/GestorOT.Client/Components/LaborAttachments.razor`
- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Api/Controllers/FilesController.cs`
- `src/GestorOT.Api/Controllers/LaborAttachmentsController.cs`
- `src/GestorOT.Domain/Entities/FileAsset.cs`
- `src/GestorOT.Domain/Entities/LaborFileAsset.cs`
- `src/GestorOT.Infrastructure/Data/Configurations/FileAssetConfiguration.cs`
- `src/GestorOT.Infrastructure/Data/Configurations/LaborFileAssetConfiguration.cs`
- DTOs de archivos en `src/GestorOT.Shared/Dtos/FileAssetDto.cs`
- Tests existentes en `src/GestorOT.Tests`

## Bugs a corregir

### Bug 1 — Biblioteca no disponible al editar labor existente

Situación actual:

- En `LaborAttachments.razor`, el botón “Biblioteca” aparece solo cuando `!HasLaborId`.
- Funcionalmente, debe estar disponible también al editar una labor existente.

Implementación requerida:

1. Mostrar botón “Biblioteca” siempre que la campaña no esté bloqueada.
2. Si `HasLaborId == true` y el usuario selecciona un archivo de biblioteca:
   - Vincularlo inmediatamente a la labor mediante endpoint backend.
   - Refrescar adjuntos.
   - Evitar duplicados.
3. Si `HasLaborId == false`:
   - Mantener el comportamiento de selección pendiente local.
4. La UI debe diferenciar visualmente archivos ya vinculados, seleccionados temporalmente y disponibles.

### Bug 2 — Al cancelar una labor nueva con archivos subidos, debe preguntar qué hacer

Spec funcional:

- Si el archivo se subió por primera vez durante la creación de la labor y el usuario cancela, debe preguntarse si desea eliminarlo o conservarlo en biblioteca.
- Si solo seleccionó un archivo existente de biblioteca, no debe eliminarse.

Implementación requerida:

1. Diferenciar archivos:
   - `UploadedInCurrentDraft`: subidos durante el formulario actual.
   - `SelectedFromLibrary`: elegidos desde biblioteca.
2. Exponer desde `LaborAttachments` un método público para saber si hay archivos subidos no vinculados.
3. En `LaborEditorForm`, interceptar cancelación/cierre del modal o drawer.
4. Mostrar confirmación:
   - “Eliminar archivos subidos no vinculados”
   - “Conservar en biblioteca”
   - “Cancelar cierre”
5. Implementar endpoint seguro para eliminar archivos no vinculados si se elige eliminar.
6. No eliminar archivos que ya estén vinculados a otra labor.

### Bug 3 — `LinkPendingFiles` oculta errores

Situación actual:

- `LinkPendingFiles` atrapa excepción y solo hace `Console.WriteLine`.
- El usuario puede creer que la labor quedó con adjuntos cuando no fue así.

Implementación requerida:

1. Cambiar `LinkPendingFiles(Guid laborId)` para devolver un resultado explícito:
   - `bool Success`
   - `List<string> Errors`
   - `int LinkedCount`
2. En `LaborEditorForm`, después de crear la labor, await del vínculo de adjuntos.
3. Si falla el vínculo:
   - Mantener mensaje claro al usuario.
   - No perder la referencia local sin avisar.
   - Ideal: mostrar opción de reintentar.

### Bug 4 — Archivos reutilizables y relación archivo → muchas labores

Validar que:

- Un mismo `FileAsset` puede asociarse a muchas `Labor` mediante `LaborFileAsset`.
- El backend evita duplicados `LaborId + FileAssetId`.
- La biblioteca lista archivos con búsqueda por nombre/tags.
- No se duplica contenido si el hash ya existe.

## Criterios de aceptación funcional

- Crear labor nueva, subir PDF antes de guardar, guardar: la labor queda con el PDF vinculado.
- Crear labor nueva, subir PDF antes de guardar, cancelar: aparece confirmación para eliminar o conservar.
- Editar labor existente: se puede abrir biblioteca y seleccionar archivo ya cargado.
- El mismo archivo se puede vincular a dos labores distintas sin duplicar contenido binario.
- En campaña bloqueada solo se puede visualizar/descargar, no subir, seleccionar ni desvincular.
- Si falla el vínculo de archivos después de crear la labor, el usuario ve error claro.

## Tests obligatorios

Agregar tests para:

1. `FilesController.Upload` sin `laborId` crea `FileAsset` sin vínculo.
2. `FilesController.Upload` con `laborId` crea o reutiliza `FileAsset` y vincula.
3. `FilesController.LinkFiles` evita duplicados y valida IDs existentes.
4. Eliminación de archivo no vinculado funciona.
5. Eliminación de archivo vinculado a una o más labores se bloquea.
6. Campaña bloqueada impide subir/vincular/desvincular.

## Restricciones técnicas

- No guardar archivos en memoria sin límite; mantener límite explícito de tamaño.
- No usar `Console.WriteLine`; usar logging o feedback de UI según corresponda.
- No romper compatibilidad con serialización source-generated.
- Mantener separación entre almacenamiento (`FileAsset`) y vínculo (`LaborFileAsset`).
- Toda acción destructiva debe pedir confirmación.

## Entregable esperado

- Código corregido.
- Tests agregados.
- Resumen con comandos ejecutados: `dotnet build`, `dotnet test`.
- Sección **Documentación consultada vía Context7**.
