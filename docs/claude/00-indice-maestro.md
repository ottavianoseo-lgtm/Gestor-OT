# Gestor OT — Índice Maestro de Sub-Planes
**GestorMax ERP** | Abril 2026 | v1.0

---

## Visión general

El plan estratégico de corrección y mejora de **Gestor OT** fue dividido en **4 sub-planes independientes**, uno por sprint. Cada archivo es autocontenido y puede entregarse directamente a un agente para su ejecución.

---

## Sub-planes

| Archivo | Sprint | Semanas | Prioridad | Issues |
|---|---|---|---|---|
| `sprint-1-bugs-criticos.md` | Sprint 1 | 1–2 | 🔴 MÁXIMA | #1 #2 #3 #4 #5 #6 #9 |
| `sprint-2-ux-consistencia.md` | Sprint 2 | 3–4 | 🟠 ALTA | #7 #8 #10 #11 #12 #13 |
| `sprint-3-mejoras-producto.md` | Sprint 3 | 5–6 | 🟡 MEDIA | #14a #14b |
| `sprint-4-hardening-multiempresa.md` | Sprint 4 | 7–8 | 🟠 ALTA | Hardening, tests, multi-tenant, docs |
| `sprint-5-uxui-details.md` | Sprint 5 | 9 | 🟡 MEDIA | UX/UI, Dashboard, Cleanup, Error Handling |
| `sprint-6-core-refact.md` | Sprint 6 | 10 | 🔴 CRÍTICA | Bug fixing, Modal stability, OT Names |

---

## Resumen de issues por sprint

### Sprint 1 — Bugs Críticos (bloqueante para producción)
- **#1** Campaña desaparece y fuerza cambio al estado previo → `CampaignProvider.razor`
- **#2** Nombre de Lote sobreescrito por nombre de Campaña → `Lotes.razor`
- **#3** Superficie Catastral sobreescrita por área GIS calculada → `Lotes.razor`
- **#4** Área Catastral no se guarda (queda en 0 al crear lote) → `LotDto.cs`
- **#5** Botones GIS se superponen al panel 'Polígono Dibujado' → `Mapa.razor`
- **#6** Lote Huérfano no se precarga al navegar desde lote sin geometría → `Mapa.razor`
- **#9** Permite guardar rotación con fechas invertidas → `RotationService.cs`

### Sprint 2 — UX, Consistencia y Estados
- **#7** Mapa hace zoom-out al asignar lote a geometría → `Mapa.razor`, `map.js`
- **#8** No hay acceso al modal de lote haciendo clic en el mapa → `Mapa.razor`, `map.js`
- **#10** Estados del dropdown de OT hardcodeados, no sincronizados con admin → `OrdenesTrabajos.razor`
- **#11** Orden visual Empresa/Campaña invertido → `MainLayout.razor`
- **#12** Renombrar 'Catálogo de Labores' y 'Superficie Catastral' → UI labels
- **#13** Tooltip de actividad bloqueada poco claro → `CampaignLotEditor.razor`

### Sprint 3 — Mejoras de Producto
- **#14a** Filtro por Campo en selector de Lotes → `CampaignLotEditor.razor`
- **#14b** Asignación masiva de Campaña desde sección Campo → `Campanias.razor`, `CampaignsController.cs`

### Sprint 4 — Hardening y Multi-Empresa
- Política multi-empresa (Tenant vs Group) → `BUSINESS_LOGIC_MASTER.md`, todos los Controllers
- Tests de regresión para bugs #1-4 → `xUnit` + `bUnit`
- Auditoría de DTOs (patrón bug #4) → todos los DTOs de POST/PUT
- Documentación del módulo GIS → `gisDoc.md`

---

## Dependencias entre sprints

```
Sprint 1  ──►  Sprint 2  ──►  Sprint 3
                                │
Sprint 4 (puede iniciar en paralelo con S3, requiere S1 y S2)
```

- **Sprint 1** es bloqueante: no hacer release de ningún sprint sin completarlo.
- **Sprint 4, Tarea A** (política multi-empresa) requiere decisión de negocio previa. Las Tareas B, C y D del Sprint 4 pueden iniciarse en paralelo con el Sprint 3.

---

## Stack de referencia

- Blazor WebAssembly + ASP.NET Core API
- EF Core + Postgres
- Leaflet / GIS
- Multi-tenant (TenantId en todos los endpoints)
