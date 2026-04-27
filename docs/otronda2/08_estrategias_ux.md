# Sub-plan 08 — Estrategias: UX y Creación de Labores

**Prioridad**: 🟢 BAJO  
**Área**: `src/GestorOT.Client/Pages/Estrategias.razor`, `src/GestorOT.Domain/Entities/StrategyItem.cs`

---

## EST-01: Campo numérico de días entre labores — etiqueta y posición

**Contexto**: El campo `DayOffset` de `StrategyItem` indica la separación en días entre labores de la estrategia. No queda claro y está mal ubicado.

**Cambios en `Estrategias.razor`**:
1. El campo de días debe moverse **entre** dos ítems de labor (no al final ni al principio de cada uno).
2. Visualización sugerida — al renderizar la lista de ítems de estrategia:
```razor
@for (int i = 0; i < _items.Count; i++)
{
    <LaborItemRow Item="_items[i]" />

    @if (i < _items.Count - 1)  // No mostrar separador después del último
    {
        <div class="day-separator">
            <div class="separator-line"></div>
            <div class="day-input-wrapper">
                <span>Días de espera</span>
                <AntDesign.InputNumber @bind-Value="_items[i + 1].DayOffset"
                    Min="0" Step="1" Style="width: 80px;" />
            </div>
            <div class="separator-line"></div>
        </div>
    }
}
```
3. El último ítem **no** tiene campo de días (lógica correcta: `i < _items.Count - 1`).

---

## EST-02: Crear Labores desde Estrategia — Flujo en 2 pasos

**Contexto**: Desde "Labor Rápida" (en Labores o WorkPlanner), el usuario puede elegir crear labores sueltas o a partir de una Estrategia.

### Paso A — Selector de entrada

Al hacer clic en "Labor Rápida" mostrar 2 opciones:
```razor
<Modal Title="Nueva Labor" Visible="_quickLaborVisible" Footer="null">
    <div style="display: flex; gap: 16px; justify-content: center; padding: 24px;">
        <Button Type="@ButtonType.Default" OnClick="OpenSingleLaborModal" Style="height: 80px; width: 160px;">
            <Icon Type="file-add" /><br/>Labor Suelta
        </Button>
        <Button Type="@ButtonType.Default" OnClick="OpenStrategyLaborModal" Style="height: 80px; width: 160px;">
            <Icon Type="apartment" /><br/>Desde Estrategia
        </Button>
    </div>
</Modal>
```

### Paso B — Modal de creación desde Estrategia (Paso 1: configuración general)

Campos del modal inicial:
- **Estado**: Planeada / Realizada
- **Estrategia**: selector de `CropStrategy` disponibles
- **Lotes**: selector múltiple de lotes de la campaña activa

Al confirmar este paso, generar vista previa.

### Paso C — Vista previa (Paso 2: edición por labor/lote)

Renderizar una lista (o cards) con todas las combinaciones `StrategyItem × Lote` como labores a crear:

**Encabezado general** (read-only):
- Estado: Planeado/Realizado
- Estrategia: [nombre]
- Actividad: [actividad de la estrategia]

**Checkbox**: "Forzar separación entre fechas según la estrategia" (usa `DayOffset`).

**Por cada labor × lote** (editable):
- Tipo de Labor (viene de `StrategyItem.LaborTypeId`, editable)
- Responsable
- Propio / Contratista
- Ha (viene del `CampaignLot.ProductiveArea` del lote en la campaña; si es mayor, advertir pero no bloquear)
- Fecha (si `AcceptsMultipleDates = false`, calcular automáticamente con `DayOffset`; si es editable, chequear rotación del lote en esa fecha y advertir si hay conflicto de actividad)

**Acciones**: `[← Atrás]` `[Crear X Labores →]`

**Backend — nuevo endpoint**:
```
POST api/labors/bulk-from-strategy
Body: {
  strategyId: Guid,
  lotIds: Guid[],
  status: LaborStatus,
  baseDate: DateTime,
  forceDateSeparation: bool,
  laborsOverride: [{ lotId, laborTypeId, contactId, hectares, date, isExternal }]
}
```

**Notas para el agente**:
- Este es el ítem más complejo del sub-plan. Puede dividirse en dos PRs: primero el selector (Paso A) + modal básico (Paso B), luego la vista previa con edición (Paso C).
- La validación de Rotación en el Paso C es la misma lógica que ya existe en `LaborEditorForm` al seleccionar fecha+lote.
