# Sub-plan 04 — Lotes: Área, Campañas y Navegación

**Prioridad**: 🟡 MEDIO  
**Área**: `src/GestorOT.Client/Pages/Lotes.razor`, `src/GestorOT.Domain/Entities/CampaignLot.cs`

---

## LOT-01: Desplegable de Lote muestra solo Ha Productivas (sin repetir Ha estándar)

**Contexto**: El "encabezado" del lote ya muestra el `CadastralArea` (Ha estándar). En el desplegable de campañas asociadas, repetir ese dato no aporta valor. Solo mostrar `ProductiveArea`.

**Archivos**: `Lotes.razor` → `<ExpandTemplate>`

**Qué hacer**:
1. En la tabla interna del desplegable (`SurfaceHistoryDto`), eliminar la columna `Ha` (CadastralArea).
2. Dejar solo: Campaña, Inicio, Sup. Productiva, Variación.
3. Actualizar `SurfaceHistoryDto` en `GestorOT.Shared/Dtos` si la columna es parte del DTO (puede dejarse en el DTO pero no renderizarla).

---

## LOT-02: Área productiva con advertencia pero sin bloqueo si supera Ha del lote

**Contexto**: Al asignar un lote a una campaña (`CampaignLot`), si `ProductiveArea > CadastralArea`, advertir pero permitir continuar.

**Archivos**: `CampaignLotEditor.razor` o el modal de edición de lote en campaña

**Qué hacer**:
1. Quitar validación que bloquea cuando `ProductiveArea > CadastralArea`.
2. Agregar validación suave con `Modal.ConfirmAsync`:
```
"El área productiva ingresada es superior a las Hectáreas indicadas para este lote. ¿Está seguro que desea continuar?"
[Sí]  [No, revisar]
```
3. Si confirma, guardar normalmente.

---

## LOT-03: Ha del lote solo acepta 2 decimales

**Archivos**: `Lotes.razor` (form de creación/edición), `CampaignLotEditor.razor`

**Qué hacer**:
1. En el `<InputNumber>` de Ha (CadastralArea y ProductiveArea):
```razor
<AntDesign.InputNumber @bind-Value="_formModel.CadastralArea"
    Min="0" Step="0.01" Precision="2" Style="width: 100%;" />
```
2. También agregar validación en backend en el DTO: el valor debe redondearse a 2 decimales antes de persistir, o agregar una data annotation `[Range]` con el formato adecuado.

---

## LOT-04: Botones GIS y Rotaciones accesibles desde el desplegable (sin abrir modal de edición)

**Contexto**: Los botones de GIS y Rotaciones solo aparecen en el modal de edición. El usuario quiere consultarlos sin riesgo de modificar datos.

**Archivos**: `Lotes.razor` → `<ExpandTemplate>`

**Qué hacer**:
1. Agregar los botones de acción GIS y Rotaciones dentro del `<ExpandTemplate>`:
```razor
<div style="display: flex; gap: 8px; margin-top: 12px;">
    <Button Size="@ButtonSize.Small" OnClick="() => OpenGisModal(lot.Data.Id)">
        <Icon Type="environment" /> GIS
    </Button>
    <Button Size="@ButtonSize.Small" OnClick="() => OpenRotationsModal(lot.Data.Id)">
        <Icon Type="sync" /> Rotaciones
    </Button>
</div>
```
2. Estos botones abren los mismos modales de solo-lectura (o de edición) que ya existen, sin necesidad de abrir el modal principal del lote.
3. Verificar que los modales de GIS y Rotaciones puedan recibir solo un `LotId` como parámetro y no dependan del estado completo del form de edición.

---

## LOT-05: Navegar al Lote específico desde el modal del mapa

**Síntoma**: El botón "Ir al Lote" desde el modal del mapa lleva solo a la lista de lotes, sin desplegar el lote en cuestión.

**Archivos**: `Mapa.razor` — botón "Ir al Lote"

**Qué hacer**:
1. Cambiar la navegación para incluir el ID del lote como query param o hash:
```csharp
Nav.NavigateTo($"/lots?highlight={lotId}");
```
2. En `Lotes.razor`, al inicializar:
```csharp
var uri = new Uri(Nav.Uri);
var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
var highlightId = query["highlight"];
if (Guid.TryParse(highlightId, out var id))
{
    // Expandir automáticamente el lote con ese ID
    _expandedLotId = id;
}
```
3. Usar el ID para auto-expandir la fila del lote correspondiente en la tabla.
