# Plan de implementación — OT-50 (cliente)

Elegir manualmente la columna de nombre al importar un shapefile. **La infraestructura ya está**;
falta solo la UI.

## Qué existe ya (verificado)

`GestorOT.Shared/Dtos/ShapefileImportDtos.cs`:

- `ShapefileFeatureDto` trae `Attributes` (`Dictionary<string,string>` con **todas** las columnas
  del `.dbf`) y `SourceShapefile`.
- `ShapefileGroupDto` trae, **por shapefile**, `AttributeColumns` y `NameColumn`.
- `ShapefileImportResultDto` expone `AttributeColumns` / `NameColumn` a nivel raíz (el primer
  grupo, por compatibilidad) más el detalle por grupo en `Groups`.

O sea: todo lo necesario ya viaja al cliente. No hace falta volver a subir el archivo ni tocar
el backend.

`Mapa.razor` ya recibe y guarda `_importGroups` (`Mapa.razor:1776`), pero no usa
`AttributeColumns` ni `NameColumn`.

## Qué falta

1. En el modal de importación, **por cada grupo** (no uno global: cada shapefile del zip tiene
   sus propias columnas), un desplegable "Columna de nombre" precargado con `NameColumn` y
   poblado con `AttributeColumns`.

2. Al cambiar la selección, **recalcular los nombres en el cliente** leyendo
   `feature.Attributes[columnaElegida]` para las features de ese grupo. Sin llamadas al backend.

3. Opción **"Sin columna de nombre"** con autonumeración `{nombreDelShapefile}_{n}`. Es el caso
   real de `Poligonos_Agricolas` del zip de La Celina: 11 features y una sola columna (`Superf`),
   ninguna de nombre. Hoy entran las 11 anónimas.

4. Vista previa con los primeros nombres resueltos, para confirmar antes de vincular.

5. Corregir el comentario desactualizado de `NameColumnCandidates`
   (`GestorOT.Infrastructure/Services/ShapefileImportService.cs:22-25`): dice que se cae a la
   primera columna de texto, y el código no hace eso.

## Trampas

1. **Trimming.** `ShapefileImportResultDto` **no** está en `AppJsonSerializerContext` y hoy se
   deserializa con `ReadFromJsonAsync<ShapefileImportResultDto>()` (`Mapa.razor:1767`), con
   `PublishTrimmed` + `TrimMode=link` activos. `Attributes` es un diccionario que el código
   cliente casi no toca: es justo el tipo de propiedad que el trimmer borra por no verla usada,
   y entonces llega vacía **solo en Release**. Es el mismo bug que tuvimos con los `Select`.
   → Registrar el DTO en el contexto y usar la sobrecarga tipada.
   → Probar con `dotnet publish -c Release`; en Debug no se reproduce.

2. **La detección automática se mantiene como default.** Si acierta, el flujo no debe sumar
   pasos: el ticket lo pide explícito.

3. **No agregar selector de columna de cultivo.** Decisión registrada en OT-49: el cultivo del
   `.dbf` no se importa.

4. Al compilar, grepear `RZ10012` además de `: error`.

## Criterios de aceptación

- El modal muestra qué columna se detectó y permite cambiarla, por shapefile.
- Cambiar la columna actualiza los nombres en la vista previa sin re-subir el archivo.
- Un shapefile sin columna de nombre ofrece autonumeración en vez de dejar todo vacío.
- Si la detección acierta, no hay pasos extra.
- El comentario de `NameColumnCandidates` queda corregido.
