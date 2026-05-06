# Plan estratégico de corrección — GestorOT

## Rama de trabajo

Repositorio detectado: `ottavianoseo-lgtm/Gestor-OT`

Rama real encontrada en GitHub: `fix/bugs-revision-gestor-ot`

## Uso obligatorio de MCP Context7

Antes de editar código en este módulo, el agente debe consultar Context7 y registrar en su reporte final qué documentación usó.

Consultas mínimas obligatorias:

```text
/context7 .NET 10 ASP.NET Core Blazor
/context7 Entity Framework Core 10
/context7 AntDesign Blazor
```

Si el módulo toca GIS, agregar:

```text
/context7 Leaflet
/context7 Leaflet.draw
/context7 Blazor JS Interop
```

Si el agente no puede usar Context7, debe detener la implementación del módulo y reportar el bloqueo. No debe improvisar cambios de APIs, componentes ni patrones.


## Objetivo

Corregir los 21 bugs reportados sin romper las reglas funcionales principales de GestorOT:

- Tenant principal por empresa.
- Contexto operativo por campaña.
- Campañas bloqueadas en modo solo lectura.
- Relación lote-campaña como base de superficies, rotaciones y labores.
- Rotaciones por lote y campaña.
- Estados dinámicos de OT definidos por el usuario.
- Planeamiento Original como baseline controlado.

## Diagnóstico desde los documentos

El documento de bugs estructura 21 errores: 2 críticos, 6 altos, 6 medios y 7 bajos. Las dependencias más importantes son:

1. BUG-16 depende de BUG-15.
2. BUG-21 comparte riesgos con campaña, lote, estado y estrategia.
3. BUG-10, BUG-11, BUG-12 y BUG-13 deben resolverse como bloque Personas/Modalidad.
4. BUG-7, BUG-18, BUG-19, BUG-20 y BUG-21 deben resolverse como bloque Estrategias.
5. BUG-2, BUG-3, BUG-6, BUG-8 y BUG-9 dependen del contexto campaña/lote.

El documento funcional confirma que todo ocurre dentro del contexto Empresa + Campaña, que una campaña bloqueada debe aparecer en selector pero ser solo lectura, y que las campañas futuras pueden seleccionarse para planear sin forzar selección automática.

## Diagnóstico desde el repositorio

Hallazgos relevantes de la rama:

1. `WorkOrdersController.CreateWorkOrder` sigue resolviendo estados por `dto.Status` y por `Name`; si falla usa fallback a `Draft`. Esto debe migrarse a `WorkOrderStatusId` como fuente principal.
2. `OrdenesTrabajos.razor` carga `api/workorder-statuses`, pero mantiene `Status` como string, KPIs por strings y fallback visual a `Draft`, `Pending`, `InProgress`.
3. `WorkOrderStatusesController` ya existe y lista estados ordenados por `SortOrder`; debe convertirse en fuente de verdad del modal, badges, edición y creación.
4. `LaborsController.GetLabors` ya soporta `campaignId`, pero `LaboresSueltas.razor` no lo agrega al query string.
5. `CampaignSelector.razor` usa `async void HandleCampaignsChanged`; debe evitarse porque puede dejar loading infinito o excepciones no capturadas.
6. `CampaignState` guarda `selected_campaign_id` en `localStorage`, pero no se observa rehidratación completa de selección.
7. `LotsController.UpdateLot` ya contempla persistir `CadastralArea` sin `WktGeometry`; BUG-08 puede estar parcialmente corregido y requiere validación UI/DTO/regresión.
8. `LaborEditorForm.razor` carga todos los campos desde `api/fields`, pero debe filtrar campos desde lotes de la campaña activa.
9. `LaboresSueltas.razor` muestra Planeamiento Original con badge `BASE`, cuando la nomenclatura funcional esperada debe ser `P.O.`.
10. `LaborsController.CreateLabor` y `UpdateLabor` deben revisarse para asegurar que `SupplyWithdrawalNotes` viaje completo entre UI, DTO, entidad, mapper y base.

## Orden recomendado

### Ola 1 — Desbloqueo crítico

1. BUG-15 — Estados dinámicos del modal de OT.
2. BUG-16 — Creación de OT.
3. BUG-21 — Confirmación de labores desde estrategia.

### Ola 2 — Campaña, lotes, superficies y GIS

4. BUG-03 — Selector de campaña colgado.
5. BUG-06 — Error falso al asignar Campaña 27/28.
6. BUG-08 — Persistencia de hectáreas.
7. BUG-05 — Formato de superficies.
8. BUG-04 — Edición GIS real.
9. BUG-02 — Labores sueltas filtradas por campaña.
10. BUG-09 — Campo/lote filtrados por campaña.

### Ola 3 — Personas, modalidad, labores y adjuntos

11. BUG-10 — Renombrar Directorio ERP a Personas y limitar modalidad.
12. BUG-11 — Unificar Directorio ERP / Personal Activo.
13. BUG-12 — Editar modalidad.
14. BUG-13 — Precargar modalidad en labores.
15. BUG-14 — Persistir Retiro de Insumos.
16. BUG-01 — Biblioteca de adjuntos.

### Ola 4 — Estrategias y Planeamiento Original

17. BUG-07 — Separador de días entre labores.
18. BUG-18 — Orden de labores.
19. BUG-19 — Filtro por campo desde estrategia.
20. BUG-20 — Eliminar labor individual.
21. BUG-17 — P.O. solo lectura, permiso especial y nomenclatura.

## Reglas globales para DeepSeek V4 Pro

1. No resolver bugs aislados si comparten modelo funcional.
2. No hardcodear estados de OT en flujos principales.
3. `WorkOrderStatusId` debe ser fuente de verdad; `Status` queda como display/compatibilidad.
4. Toda query de labores debe soportar campaña activa.
5. No crear, editar ni eliminar datos operativos si la campaña está bloqueada.
6. Nunca cambiar automáticamente la campaña seleccionada al crear una campaña nueva.
7. Los formularios deben mostrar solo campos/lotes vinculados a la campaña activa.
8. Las superficies visibles se muestran con 2 decimales.
9. Toda asignación lote-campaña valida tenant, lote, campaña y borrado lógico si existiera.
10. Cada módulo debe cerrar con pruebas manuales y evidencia de compilación.

## Definition of Done global

- Compila la solución completa.
- Se puede crear una OT con estado dinámico configurado por el usuario.
- Se pueden generar labores desde estrategia.
- Labores, lotes, campos y OTs respetan campaña activa.
- Campañas bloqueadas quedan en solo lectura.
- No quedan errores silenciosos en modales.
- Cada bug tiene prueba manual documentada.
- El reporte final lista archivos modificados, riesgos y pruebas.
