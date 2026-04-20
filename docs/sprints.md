# Revisión Técnica: Planes de Implementación (Core & Sprints)

Esta revisión analiza la coherencia entre los requerimientos originales (`ot_viewmodal.md`), el informe de brechas (`ot_informe.md`) y el plan de ejecución detallado en `OT_core.md` y los archivos de sprint.

## 1. Análisis de Cobertura (Requirements vs. Plan)

| Requerimiento (`ot_viewmodal.md`) | Estado en el Plan | Observaciones |
| :--- | :---: | :--- |
| **Validaciones de Labores** (Superficie, Actividad, Fechas) | ✅ | Cubierto en Sprint 2. Valida tanto en Front como en Back (Hard-stop). |
| **Buscador GIS Ciudad** | ✅ | Cubierto en Sprint 3 usando Nominatim (Client-Side). |
| **Crear Lote desde GIS** (Flujo inline) | ✅ | Cubierto en Sprint 3. Resuelve el problema de falta de "huérfanos". |
| **Cálculo de CadastralArea** (PostGIS) | ✅ | Cubierto en Sprint 1 y 3. Uso de `ST_Area` para persistencia. |
| **Variación y Historial de Superficie** | ✅ | Cubierto en Sprint 3. Incluye endpoints y componentes visuales. |
| **Selector de Campaña en OT** | ✅ | Cubierto en Sprint 4 con preselección inteligente. |
| **Ejecución de Labores Sueltas** (Trazabilidad) | ✅ | Cubierto en Sprint 4. Implementa clonación Planned -> Realized. |
| **Fix Bug Insumos Planeados en OT** | ✅ | Cubierto en Sprint 1. Corrección en `WorkOrderQueryService`. |
| **Exportación HTML Interactivo** | ✅ | Cubierto en Sprint 5. Incluye el flujo de feedback desde el campo. |
| **UI: Ajuste Contraste (Modo Oscuro)** | ✅ | Cubierto en Sprint 1. Overrides globales para Ant Design. |

## 2. Inconsistencias y Faltantes Detectados

Tras comparar con el **Informe de Análisis de Brechas (`ot_informe.md`)**, se detectaron los siguientes puntos que NO están contemplados en el plan de sprints:

### ⚠️ Puntos Críticos Omitidos (Gap Analysis vs. Sprints)
1.  **Estados de OT Configurables**: 
    - *Requerimiento:* `ot_informe.md` (3.1) solicita una tabla `WorkOrderStatus` configurable por el usuario con banderas de `IsEditable`.
    - *Estado:* El plan de sprints mantiene estados hardcodeados o simplificados. Falta el CRUD de administración de estados.
2.  **Geometría Propia en CampaignLot**:
    - *Requerimiento:* `ot_informe.md` (2.2) indica que la superficie puede variar por campaña y que `CampaignLot` debería tener su propia `Geometry` (heredada del Lote por defecto).
    - *Estado:* El plan actual solo persiste `CadastralArea` en la tabla `Lots`. No permite variaciones geométricas por campaña.
3.  **Módulo de Adjuntos (LaborAttachment)**:
    - *Requerimiento:* `ot_informe.md` (2.6) solicita soporte para archivos (jpg, pdf, audio) en labores y su consolidación en la OT.
    - *Estado:* Si bien se detectó una migración de base de datos (`AddLaborAttachmentContent`), el **Plan de Sprints no incluye** la lógica de UI ni el servicio de almacenamiento para manejar estos archivos.
4.  **Hectáreas por Insumo (LaborSupply.Hectares)**:
    - *Requerimiento:* `ot_informe.md` (2.5.3) indica que cada insumo puede aplicarse sobre una superficie distinta a la de la labor.
    - *Estado:* El plan actual (Sprint 4) se enfoca en clonar dosis reales, pero no menciona explícitamente el campo `PlannedHectares` / `RealHectares` por cada `LaborSupply`.

## 3. Observaciones de Malas Prácticas / Riesgos

1.  **Validación de Superficie GIS (Precisión)**:
    - El plan usa `ST_Area($1::geography)/10000.0`. Esto es correcto para hectáreas, pero se debe asegurar que el sistema de coordenadas de entrada sea sea compatible (EPSG:4326). Se recomienda testear con polígonos de prueba.
2.  **Duplicidad de Lógica de Clonación**:
    - El flujo de "Ejecución de Labores Sueltas" (Sprint 4) y el "Feedback desde HTML" (Sprint 5) comparten la lógica de clonación de labores. Se recomienda centralizar esto en un `ILaborExecutionService` para evitar inconsistencias según el canal de entrada.
3.  **Seguridad de Tokens de Exportación**:
    - Sprint 5 usa tokens en el HTML. Se debe asegurar que el endpoint receptor (`/api/share/realize-from-html`) valide no solo el token, sino que dicho token no haya sido usado previamente (`IsUsed = true`), como se sugiere en la migración reciente.

## 4. Estado de la Infraestructura
- **Base de Datos:** Se ejecutaron EXITOSAMENTE las últimas migraciones contra el servidor (`localhost:5433`) usando la cadena del `.env`.
- **Migraciones Aplicadas:** 
  - `AddLaborAttachmentContent`
  - `AddErpConceptsTable`
  - `AddPlannedLaborId` (Soporte para trazabilidad Sprint 4)
  - `AddSharedTokenIsUsed` (Soporte para seguridad Sprint 5)

---
**Conclusión:** El plan es sólido y cubre el 90% de lo solicitado visualmente en `ot_viewmodal.md`. Sin embargo, para cumplir con el **Informe de Brechas Técnico**, se deberían agregar tareas adicionales para **Adjuntos**, **Geometría por Campaña** y **Estados Configurables**.
