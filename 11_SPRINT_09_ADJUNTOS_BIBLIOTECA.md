# Sprint 09 - Adjuntos antes de guardar y biblioteca reutilizable

## Objetivo

Permitir adjuntar archivos antes de guardar una labor y transformar los adjuntos en una biblioteca reutilizable.

## Rama sugerida

`fix/s09-adjuntos-biblioteca`

## Bugs cubiertos

- Solo se puede adjuntar archivo después de crear la labor.
- El mensaje "Podés adjuntar archivos una vez guardada la labor" contradice el requerimiento.
- No hay biblioteca reutilizable.
- Un archivo puede terminar duplicado si se usa en varias labores.

## Archivos principales

- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Client/Components/LaborAttachments.razor`
- `src/GestorOT.Api/Controllers/LaborAttachmentsController.cs`
- Entidad actual `LaborAttachment`
- Configuración EF `LaborAttachmentConfiguration`
- `IApplicationDbContext`
- `ApplicationDbContext`
- DTOs de adjuntos
- Migraciones EF Core

## Regla funcional

El archivo debe existir como activo reutilizable y luego vincularse a labores u OTs.

## Diseño técnico sugerido

### Entidad FileAsset

Campos sugeridos:

- `Id`
- `TenantId`
- `FileName`
- `MimeType`
- `SizeBytes`
- `Content` o `StorageKey`
- `UploadedAt`
- `UploadedBy`
- `Hash`
- `Tags`
- `Visibility`

### Entidad LaborFileAsset

Campos sugeridos:

- `LaborId`
- `FileAssetId`
- `LinkedAt`

### Entidad WorkOrderFileAsset

Campos sugeridos:

- `WorkOrderId`
- `FileAssetId`
- `LinkedAt`

### Entidad UploadSession o DraftAttachment

Campos sugeridos:

- `Id`
- `SessionId`
- `FileAssetId`
- `CreatedAt`
- `ExpiresAt`
- `CreatedBy`

## Tareas técnicas

### 1. Compatibilidad con datos actuales

1. No borrar adjuntos actuales.
2. Crear migración para nueva biblioteca.
3. Si es posible, migrar registros existentes:
   - cada `LaborAttachment` actual pasa a `FileAsset`;
   - crear relación `LaborFileAsset`.
4. Mantener endpoint legacy temporal si hace falta.

### 2. API

Crear endpoints claros:

- Subir archivo a biblioteca.
- Listar biblioteca.
- Buscar por nombre/tag.
- Vincular archivo a labor.
- Desvincular archivo de labor.
- Vincular archivos temporales al guardar labor.
- Descargar archivo.
- Eliminar archivo si no está vinculado o si el usuario confirma.

### 3. UI LaborEditorForm

Cuando `LaborId == Guid.Empty`:

1. Mostrar sección de adjuntos igual.
2. Permitir:
   - subir archivo nuevo;
   - elegir archivo existente de biblioteca.
3. Mantener lista local:
   - archivos nuevos subidos;
   - archivos existentes seleccionados.
4. Al guardar labor:
   - crear labor;
   - vincular archivos.
5. Al cancelar:
   - si hay archivos nuevos, preguntar:
     - eliminar;
     - conservar en biblioteca;
   - si son archivos existentes, solo quitar selección.

### 4. UI LaborAttachments

1. Separar componente en:
   - modo labor existente;
   - modo pre-guardado.
2. Mostrar estado de cada archivo:
   - nuevo;
   - existente;
   - vinculado.
3. Permitir descargar si ya está en biblioteca.
4. Permitir quitar de la selección.

### 5. Límites

1. Mantener límite de tamaño razonable.
2. Validar tipo MIME.
3. Mensajes claros para archivo grande.
4. Evitar duplicados por hash si se implementa hash.

## No hacer en este sprint

- No implementar almacenamiento externo si no está en alcance.
- No rediseñar todas las evidencias.
- No modificar ejecución pública salvo que use adjuntos.

## Pruebas manuales

### Caso 1 - Adjuntar antes de guardar

1. Abrir nueva labor.
2. Subir PDF.
3. Guardar labor.

Resultado esperado:

- La labor se crea.
- El PDF queda vinculado.
- El PDF queda en biblioteca.

### Caso 2 - Cancelar

1. Abrir nueva labor.
2. Subir archivo.
3. Cancelar.

Resultado esperado:

- Pregunta si eliminar o conservar.
- Si conservar, queda en biblioteca.
- Si eliminar, no queda archivo huérfano.

### Caso 3 - Reutilizar archivo

1. Crear labor A con archivo.
2. Crear labor B.
3. Elegir archivo existente.

Resultado esperado:

- No duplica contenido.
- El archivo queda vinculado a A y B.

### Caso 4 - Archivo grande

1. Subir archivo mayor al límite.

Resultado esperado:

- Error claro.
- No rompe formulario.

## Criterios de aceptación

- Se puede adjuntar antes de guardar.
- Biblioteca reutilizable funcionando.
- Archivos existentes no se duplican innecesariamente.
- Cancelación resuelve archivos temporales.
- Migración documentada.
- Código compila.

## Prompt corto para DeepSeek

Implementá solo Sprint 09. Convertí adjuntos de labor en biblioteca reutilizable y permití adjuntar antes de guardar una labor. Crear archivo debe ser independiente de crear labor; al guardar se vinculan archivos. Al cancelar, preguntar si eliminar o conservar archivos nuevos. Mantené compatibilidad con adjuntos existentes y documentá migración.
