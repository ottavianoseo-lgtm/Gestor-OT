# Plan de implementación — OT-48 (cliente)

Vinculación masiva de polígonos importados a lotes. **El backend ya está hecho y mergeado**
(`9517b84`); falta únicamente la pantalla de conciliación.

## Qué existe ya (verificado)

| Pieza | Dónde |
|---|---|
| `POST api/lots/match` → propuesta por feature | `GestorOT.Api/Controllers/LotsController.cs` |
| `POST api/lots/bulk-link` → aplica todo o nada | idem |
| Lógica de matcheo y aplicación | `GestorOT.Infrastructure/Services/LotBulkLinkService.cs` |
| DTOs | `GestorOT.Shared/Dtos/` (`LotMatchRequestDto`, `LotMatchResultDto`, `LotMatchProposalDto`, `LotBulkLinkRequestDto`, `LotBulkLinkResultDto`) |
| Tests | `GestorOT.Tests/Regression/LotBulkLinkTests.cs` (10 casos) |

El matcheo ya está acotado al campo destino y normaliza nombres (mayúsculas, espacios, ceros a
la izquierda). El `bulk-link` chequea solapamientos **antes** de abrir la transacción y aplica
todo o nada.

## Qué falta

La pantalla. Hoy, después de importar, `Mapa.razor` sigue ofreciendo vincular polígono por
polígono. Los dos endpoints no se llaman desde ningún lado del cliente.

## Pasos

1. **Registrar los DTOs en el contexto JSON** — ver la trampa 1 más abajo. Es el primer paso,
   no el último.

2. **Elegir campo destino antes de conciliar.** `POST api/lots/match` rechaza `FieldId` vacío a
   propósito: los `.dbf` reales traen el lote como `"1"`, `"2"`, sin prefijo del
   establecimiento, así que un cruce global colisiona entre campos. Si el modal de importación
   ya tiene un selector de campo, reusarlo; si no, agregarlo como paso previo obligatorio.

3. **Llamar a `match`** con las features importadas y renderizar una tabla de conciliación, una
   fila por feature:

   | Columna | Contenido |
   |---|---|
   | Feature | `FeatureName` + `SourceShapefile` (el zip puede traer varios) |
   | Superficie | `AreaHa` |
   | Estado | `ExactMatch` / `Ambiguous` / `NoMatch` |
   | Acción | desplegable: Vincular / Crear / Omitir (precargado con `SuggestedAction`) |
   | Lote destino | si `ExactMatch`, `MatchedLotName`; si `Ambiguous`, un select con `Candidates` |
   | Aviso | si `TargetHasGeometry`, marcar que se va a pisar una geometría existente |

4. **Resumen arriba de la tabla**: `ToLink` a vincular, `ToCreate` a crear, `Ambiguous` a
   resolver. Deshabilitar el botón de confirmar mientras haya filas en `Ambiguous` sin decidir.

5. **Confirmar** con `POST api/lots/bulk-link`, mandando `FieldId`, los `Items` (con la acción
   final de cada fila), `CampaignId` si hay campaña activa, y los flags `CombineGeometry` /
   `OverrideOverlap`.

6. **Manejar la respuesta.** Devuelve `200` con `Success=true`, o **`409`** con `Success=false`
   y `OverlapWarnings` cargado. El 409 no es un error: es "hay conflictos, decidí". Mostrar los
   solapamientos agrupados en un solo aviso —no un modal por lote, que es justamente lo que el
   ticket quiere eliminar— con la opción de reenviar con `OverrideOverlap = true`.

7. Al terminar, recargar el mapa (`ReloadMap`) para que se vean las geometrías nuevas.

## Trampas

1. **Trimming — la más importante.** El cliente publica con `PublishTrimmed` y
   `TrimMode=link` (`GestorOT.Client.csproj:15-16`). Ninguno de los DTOs de OT-48 está en
   `GestorOT.Shared/AppJsonSerializerContext.cs`. Si se deserializa con
   `ReadFromJsonAsync<T>()` a secas, funciona en Debug y **puede romper en Release**, porque el
   trimmer borra los getters que solo se usan por reflexión. Es exactamente el bug que ya nos
   costó una tarde con los `Select` de la config contable.
   → Agregar `[JsonSerializable(typeof(...))]` para cada DTO y usar las sobrecargas tipadas
   (`AppJsonSerializerContext.Default.LotMatchResultDto`), como ya hace el resto de `Mapa.razor`.
   → Verificar con `dotnet publish -c Release`, no solo con `dotnet build`.

2. **Precedente ya presente en el archivo**: `ReadFromJsonAsync<ShapefileImportResultDto>()`
   (`Mapa.razor:1767`) tiene hoy ese mismo problema sin resolver. Conviene arreglarlo de paso.

3. **RZ10012.** Al compilar, filtrar explícitamente por `RZ10012`, no solo por `: error`. Un
   componente mal escrito compila sin error y se renderiza como markup inerte. Ya pasó con
   `RadioButton`, y hoy sigue pasando en `Home.razor:154` con `LotDetailModal`.

4. **El flujo de a uno tiene que seguir andando.** Sirve para correcciones puntuales y es
   criterio de aceptación del ticket.

## Criterios de aceptación

- Importar un shapefile con N features muestra la tabla con el estado propuesto de cada una.
- El cruce se hace dentro del campo elegido, no global.
- Cualquier fila se puede corregir antes de confirmar.
- Un solo botón aplica todo.
- Los solapamientos se reportan agrupados.
- Si falla un ítem, el resultado dice cuál y por qué, y no queda nada a medio aplicar.
- Vincular un polígono a mano sigue funcionando.
- Cargar los 4 lotes de La Celina lleva una sola confirmación.
