# ⚙️ NÚCLEO DE ÓRDENES DE TRABAJO Y LABORES (CRÍTICO)

## 1. MODELO DE DATOS OBLIGATORIO
El sistema actual es insuficiente. Se debe implementar esta jerarquía en `GestorOT.Server` y `Shared`:

### Entidad: Labor (La Tarea)
- **Id:** Guid.
- **WorkOrderId:** FK (Nullable, una labor puede existir sin OT y asignarse luego).
- **CampaignLotId:** FK (El lote productivo donde se hace).
- **LaborType:** Enum/Tabla (Siembra, Pulverización, Cosecha).
- **Status:** Planned | Realized.
- **ExecutionDate:** DateTime.
- **Hectares:** decimal (Puede diferir del tamaño del lote si se hace parcial).
- **LaborSupplies:** Colección de insumos.

### Entidad: LaborSupply (Los Insumos)
- **SupplyId:** FK (El producto, ej: Glifosato).
- **PlannedDose:** decimal (Dosis objetivo).
- **RealDose:** decimal (Lo que realmente se usó).
- **TotalQuantity:** Calculado (`Dose * Labor.Hectares`).

## 2. REGLAS DE NEGOCIO (LOGICA DE DOMINIO)
1. **Inmutabilidad del Plan:** Al pasar una labor de "Planned" a "Realized", si hay cambios, se debe guardar la discrepancia, no borrar el plan original.
2. **Cálculo Automático:** Si el usuario cambia la Dosis, se recalcula el Total. Si cambia el Total, se recalcula la Dosis inversa.
3. **Validación de Lote:** Una labor no puede crearse si el `CampaignLot` no tiene una campaña activa asignada.

## 3. EXPERIENCIA DE USUARIO (UX)
- **Wizard de Carga:** El formulario de OT debe tener una grilla editable (Table inline editing) para cargar Labores e Insumos rápidamente.
- **Botón "Replicar":** Acción para copiar una Labor "Planeada" y convertirla en "Realizada" con un solo clic, permitiendo ajustar solo la diferencia.
