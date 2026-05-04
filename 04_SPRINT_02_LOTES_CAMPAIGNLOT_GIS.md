# Sprint 02 - Lotes, CampaignLot, superficie productiva y GIS

## Objetivo

Corregir la relación entre lote físico y campaña. Un lote puede participar en varias campañas, y la superficie productiva pertenece a la relación `Lote + Campaña`.

## Rama sugerida

`fix/s02-lotes-campaignlot-gis`

## Bugs cubiertos

- Selector campaña por lote muestra campañas ya utilizadas.
- Lote con dos campañas muestra una sola.
- Hectáreas mayores al lote no deben bloquear.
- No se puede editar polígono de un lote ya creado.
- Falta acceso cómodo a rotaciones por lote/campaña.

## Archivos principales

- `src/GestorOT.Client/Pages/Lotes.razor`
- `src/GestorOT.Api/Controllers/CampaignsController.cs`
- `src/GestorOT.Api/Controllers/LotsController.cs`, si existe
- `src/GestorOT.Domain/Entities/Lot.cs`
- `src/GestorOT.Domain/Entities/CampaignLot.cs`
- `src/GestorOT.Shared/Dtos/LotDto.cs`
- `src/GestorOT.Shared/Dtos/CampaignLotDto.cs`

## Regla funcional

- `Lot.CadastralArea`: superficie física base.
- `CampaignLot.ProductiveArea`: superficie usada en una campaña.
- Un `LotId` puede tener muchos `CampaignLotId`.
- Las operaciones de labores y rotaciones deben usar `CampaignLotId`.

## Tareas técnicas

### 1. Separar listas funcionales

En UI/backend debe quedar claro:

1. Campañas globales: para selector general.
2. Campañas asignadas al lote: relaciones existentes.
3. Campañas asignables al lote: activas, no bloqueadas y no ya asignadas.

### 2. Modal de asignación campaña-lote

En `Lotes.razor`:

1. Al abrir el modal de asignar campaña a lote, cargar campañas asignables.
2. Excluir campañas ya presentes en `_lotCampaigns`.
3. Excluir campañas bloqueadas.
4. Excluir campañas inactivas para nuevas asignaciones.
5. Si no hay campañas disponibles, mostrar mensaje claro.

### 3. Mostrar todas las campañas del lote

1. No deduplicar por `LotId`.
2. Usar `CampaignLotDto.Id` o `CampaignLotId` como key de la fila.
3. Mostrar:
   - campaña;
   - superficie productiva;
   - variación contra catastral;
   - botón guardar superficie;
   - botón rotaciones;
   - botón quitar relación si la campaña no está bloqueada.

### 4. Superficie productiva mayor a catastral

1. Si `ProductiveArea > CadastralArea`, mostrar advertencia.
2. No bloquear.
3. Pedir confirmación antes de guardar.
4. Guardar si el usuario confirma.
5. Mostrar variación con dos decimales.

### 5. GIS

En la columna de geometría:

1. Si no tiene geometría, mostrar `Configurar GIS`.
2. Si tiene geometría, mostrar `Editar GIS`.
3. Ambos deben navegar al editor de mapa con `lotId`.
4. El editor debe permitir modificar el polígono existente.

### 6. Acceso a rotaciones

1. Cada relación campaña-lote debe tener acceso `Rotac.`.
2. El acceso debe pasar `CampaignId` y `CampaignLotId`, no solo `LotId`.
3. Si la campaña está bloqueada, abrir rotaciones en solo lectura.

## No hacer en este sprint

- No corregir editor de labores completo.
- No implementar reglas agronómicas profundas.
- No tocar estrategias.
- No rediseñar mapa completo salvo habilitar edición.

## Pruebas manuales

### Caso 1 - Lote con dos campañas

1. Crear o usar lote `Lote A`.
2. Asignarlo a campaña `23/24`.
3. Asignarlo a campaña `26/27`.
4. Editar lote.

Resultado esperado:

- Se ven ambas campañas.
- Cada una tiene su superficie productiva.
- Cada una tiene botón de rotaciones.

### Caso 2 - Selector sin campañas repetidas

1. Abrir modal para agregar campaña a `Lote A`.

Resultado esperado:

- No aparecen campañas ya asignadas a ese lote.

### Caso 3 - Superficie mayor a catastral

1. Lote catastral 100 ha.
2. Asignar o editar superficie productiva 101 ha.
3. Confirmar.

Resultado esperado:

- Advierte.
- Permite guardar.
- Muestra variación `+1,00 ha`.

### Caso 4 - Editar GIS

1. Lote con geometría.
2. Click `Editar GIS`.

Resultado esperado:

- Abre editor con polígono existente.
- Permite editarlo.

## Criterios de aceptación

- Un lote puede verse en varias campañas.
- No se repiten campañas en selector de asignación.
- `ProductiveArea > CadastralArea` no bloquea.
- GIS editable aunque ya exista polígono.
- El código compila.
- No se rompió el selector global de campaña.

## Prompt corto para DeepSeek

Implementá solo Sprint 02. Corregí la pantalla de Lotes para respetar la relación `CampaignLot`: mostrar todas las campañas del lote, excluir campañas ya usadas al asignar, permitir superficie productiva mayor con advertencia y habilitar edición GIS aunque el lote ya tenga geometría. No toques labores ni estrategias salvo contratos mínimos.
