# Plan de implementación — OT-26

Importación de labores: contemplar responsable asignado y proveedor de insumos.

## Estado real: no está hecho

El ticket figura **En curso**, pero en `LaborExcelImportService.cs` no hay nada de responsable
ni de proveedor de insumos. Un grep de `responsable` / `AssignedTo` / `proveedor` / `supplier`
sobre ese archivo no devuelve nada.

Lo que **sí** existe y conviene no confundir: el parser ya detecta una columna
`"Contr/Prove" | "contratista" | "maquinaria"` (`LaborExcelImportService.cs:665`) y la guarda
como `ColContratista` (`:684`). Pero se usa **solo** para clasificar la labor como propia o de
contratista (`:1380`, `norm.Contains("contrat")`), no para asignar una persona ni un proveedor.

## Los dos casos del ticket

1. **Responsable asignado.** Cuando el Excel trae quién ejecuta la labor, hoy se pierde.
2. **Proveedor de insumos.** Cuando se declaran insumos y además quién los provee, hoy se pierde.

## Pasos

1. **Decidir primero si son el mismo dato o dos distintos.** La columna que ya se lee se llama
   "Contr/Prove" — contratista *y* proveedor en una sola. Antes de escribir código hay que mirar
   Excels reales y confirmar si el archivo trae una columna o dos, y si "responsable" es una
   persona (`Contact`) o el contratista de la labor. **Esto es lo primero y condiciona todo el
   resto**; sin eso se implementa a ciegas.

2. **Parser.** Si son columnas distintas, agregar la detección de la de responsable junto a las
   demás (`:649-666`), siguiendo el patrón de comparación en minúsculas ya usado.

3. **DTOs.** Extender `LaborImportParsedLaborDto` (responsable) y `LaborImportParsedItemDto`
   (proveedor del insumo) en `GestorOT.Shared/Dtos/LaborImportDtos.cs`. Campos **aditivos con
   default**, para no romper el contrato existente.

4. **Resolución contra el padrón.** El nombre del Excel hay que cruzarlo contra `Contacts`
   (`ErpPerson` / `Contact`). Conviene reutilizar el mecanismo de alias que ya existe para
   insumos y labores (`SupplyAlias`, `LaborTypeAlias`) en vez de inventar otro: el matcheo por
   nombre libre falla igual que falló ahí.

5. **Vista previa.** El modal de importación (`LaborImportModal.razor`) tiene que mostrar el
   responsable y el proveedor resueltos, y permitir corregirlos antes de confirmar — igual que
   ya se hace con los tipos de labor y los insumos.

6. **Persistencia.** Mapear el responsable a `Labor.ContactId` y el proveedor al `LaborSupply`
   correspondiente. Verificar antes qué campo del `LaborSupply` corresponde: si no existe, es
   una migración y hay que decidirlo explícitamente.

## Trampas

- **Un nombre no resuelto no debe abortar la importación entera.** El patrón del importador es
  previsualizar, dejar corregir y recién después aplicar.
- Si el responsable no viene, tiene que quedar vacío sin romper: relacionado con OT-31, donde
  el planteo es justamente que el responsable no siempre existe.
- El import corre dentro de una transacción (`LotExcelImportService.ExecuteAsync` es el
  precedente): un fallo a mitad no puede dejar labores a medio crear.
- Filtros de tenant: resolver contactos e insumos sin `IgnoreQueryFilters()`.

## Criterios de aceptación

- Un Excel con responsable lo importa y queda asignado en la labor.
- Un Excel con proveedor de insumo lo importa y queda asociado al insumo de esa labor.
- Un nombre que no matchea se muestra en la vista previa para corregirlo, no rompe el import.
- Un Excel sin esas columnas se importa exactamente como hoy (test de regresión).
