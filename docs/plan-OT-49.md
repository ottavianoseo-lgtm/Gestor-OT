# Plan de implementación — OT-49

Geometría por campaña: persistir el polígono del año en `CampaignLot` y derivar de él la
superficie real.

## Estado real: no está empezado

El ticket figura **En curso** en Jira, pero en el código no hay nada salvo la columna:

- `CampaignLot.Geometry` existe en la entidad (`GestorOT.Domain/Entities/CampaignLot.cs`) y en
  el snapshot de EF (`ApplicationDbContextModelSnapshot.cs:276`, `geometry(Geometry, 4326)`), así
  que **la columna ya está en la base y no hace falta migración para guardarla**.
- Nadie la lee ni la escribe. Un grep de `cl.Geometry` / `CampaignLot.*Geometry` fuera de la
  entidad y las migraciones no devuelve **ningún** resultado.

## Por qué importa

Hoy `ProductiveArea` se inicializa copiando `CadastralArea`
(`CampaignManagerService.cs:173`, `LotsController.cs:159`), así que superficie real y teórica
casi siempre coinciden y la distinción es cosmética. Este ticket es el que las separa de verdad.

Ojo: **OT-51 ya limpió los fallbacks** que enmascaraban el problema (las labores ya no se
dimensionan con `CadastralArea`). O sea, el terreno está preparado.

## Pasos

1. **Escritura.** Definir por dónde entra el polígono del año. Lo natural es el flujo de
   importación/vinculación (`LotBulkLinkService.ApplyAsync` ya crea/actualiza el `CampaignLot`
   cuando viene `CampaignId`): ahí, además de la superficie, guardar la geometría en
   `CampaignLot.Geometry`.

2. **Derivar la superficie.** `ProductiveArea` pasa a calcularse con PostGIS sobre
   `CampaignLot.Geometry` cuando existe, y a caer en la del lote cuando no.
   Usar `LotQueryService.CalculateAreaFromWktAsync` — es la fuente de verdad y **ya no tiene el
   fallback en memoria** que devolvía superficies inventadas (se eliminó en `d6689d8`).

3. **Lectura.** Decidir qué muestra el mapa cuando hay geometría de campaña: la del año o la
   del lote. Sugerido: la de la campaña si existe, porque es la que corresponde a lo que se
   trabajó. Impacta `LotQueryService.GetGeoJsonAsync`, que hoy solo mira `Lot.Geometry`.

4. **Efecto retroactivo — el punto delicado, ya acordado en OT-51.** Cuando la superficie real
   baje al recalcularse desde el polígono, van a quedar labores cargadas que exceden la
   superficie del lote. La regla acordada:
   - la superficie real manda: **no** se bloquea guardar la geometría por labores preexistentes;
   - las labores excedidas se **reportan** como inconsistencia, no se corrigen ni se borran solas;
   - falta definir dónde se muestran (listado tras el import, indicador en el lote, o ambos).

5. **Tests.** Al menos: que `ProductiveArea` salga del polígono de campaña cuando existe; que
   caiga en la del lote cuando no; y que bajar la superficie reporte las labores excedidas sin
   bloquear el guardado.

## Trampas

- `CampaignLot` tiene filtro global de tenant. No agregar `IgnoreQueryFilters()`.
- No recalcular áreas en el cliente. La superficie sale de `ST_Area(geography)` y de ningún otro
  lado; un segundo cálculo en JS da un número distinto al del panel, los reportes y los pases.
- `CampaignLot.CropId` es una columna huérfana (sin FK, sin navegación, sin uso). No usarla.
- El cultivo del `.dbf` **no** se importa: decisión ya registrada.

## Antes de empezar

El ticket está en prioridad Low y es el más invasivo de los que quedan. Conviene confirmar con
el equipo el punto 3 (qué geometría muestra el mapa) y el 4 (dónde se reportan las
inconsistencias) antes de escribir código: son decisiones de producto, no de implementación.
