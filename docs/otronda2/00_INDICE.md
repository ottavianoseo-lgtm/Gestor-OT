# GestorOT — Plan de Iteración Ronda 2

## Stack del proyecto
- **Backend**: ASP.NET Core Web API (C#), Entity Framework Core, PostgreSQL + PostGIS
- **Frontend**: Blazor WebAssembly, AntDesign Blazor
- **Arquitectura**: Clean Architecture (Domain / Application / Infrastructure / Api / Client)
- **Multi-tenancy**: filtrado por `TenantId` via interceptors

## Archivos clave de referencia
| Archivo | Propósito |
|---|---|
| `src/GestorOT.Client/Pages/Lotes.razor` | Página de gestión de lotes (808 líneas) |
| `src/GestorOT.Client/Pages/OrdenesTrabajos.razor` | Lista y modal de OTs |
| `src/GestorOT.Client/Pages/LaboresSueltas.razor` | Lista de labores |
| `src/GestorOT.Client/Pages/WorkPlanner.razor` | Planificador temporal |
| `src/GestorOT.Client/Pages/Estrategias.razor` | Gestión de estrategias |
| `src/GestorOT.Client/Components/LaborEditorForm.razor` | Modal/form de Labor |
| `src/GestorOT.Domain/Entities/` | Entidades de dominio |
| `src/GestorOT.Api/Controllers/` | Endpoints REST |

## Sub-planes (orden de prioridad)

| # | Archivo | Área | Impacto |
|---|---|---|---|
| 1 | `01_bugs_criticos.md` | Bugs que rompen funcionalidad core | 🔴 Crítico |
| 2 | `02_ot_modal_y_tabla.md` | Mejoras al modal y tabla de OTs | 🔴 Alto |
| 3 | `03_labores_mejoras.md` | Mejoras al modal y flujo de Labores | 🔴 Alto |
| 4 | `04_lotes_campana_area.md` | Lotes: área productiva y desplegable | 🟡 Medio |
| 5 | `05_work_planner_mejoras.md` | Work Planner: filtros y consistencia | 🟡 Medio |
| 6 | `06_bloqueo_ot_estados.md` | Bloqueo de edición por estado de OT | 🟡 Medio |
| 7 | `07_labores_sueltas_refactor.md` | LaboresSueltas: mostrar todas + filtros | 🟡 Medio |
| 8 | `08_estrategias_ux.md` | Estrategias: UX de días y creación de labores | 🟢 Bajo |
| 9 | `09_planeamiento_original.md` | Módulo de Planeamiento Original (read-only) | 🟢 Bajo |

---

## Convenciones para el agente
- Siempre respetar multi-tenancy: todos los endpoints filtran por `TenantId`.
- Usar `IMessageService` de AntDesign para toasts de confirmación/error.
- Los modales usan el patrón `_modalVisible` + `_formModel` ya establecido en el proyecto.
- Validaciones de negocio que NO bloquean al usuario → `Modal.Confirm` con Sí/No.
- Validaciones que SÍ bloquean → `Message.Warning` y retorno temprano.
- Los DTOs viven en `GestorOT.Shared/Dtos`; si se agrega un campo nuevo al DTO hay que actualizar también el controller y el mapping.
