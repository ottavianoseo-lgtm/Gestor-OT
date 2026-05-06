# Módulo 05 — Planeamiento Original

Bug incluido: BUG-17.

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


## Objetivo funcional

El Planeamiento Original es una línea de base para comparar plan vs real. Debe ser de solo lectura por defecto y editable únicamente por usuarios con permiso especial. La nomenclatura visual debe ser consistente: `P.O.`.

## Archivos a revisar

- `src/GestorOT.Client/Pages/LaboresSueltas.razor`
- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- WorkPlanner o pantalla equivalente.
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- Entidad `Labor`.
- DTO `LaborDto`.
- Autorización/roles/permisos.
- `AuditLog`.

## Reglas funcionales

1. Una labor de Planeamiento Original siempre debe ser planeada.
2. No puede nacer como realizada.
3. Por defecto no se edita.
4. Solo un administrador con permiso explícito puede desanclar o modificar.
5. Desanclar significa quitar `IsOriginalPlan`.
6. Debe quedar auditoría.
7. El texto visible debe ser `P.O.`, no `BASE`.

## Implementación requerida

### UI

1. Cambiar badge `BASE` por `P.O.` en todas las vistas.
2. Si `IsOriginalPlan == true`, bloquear acciones de edición por defecto.
3. Mostrar tooltip: “Planeamiento Original: solo lectura”.
4. Mostrar acción “Desanclar P.O.” solo para usuario autorizado.
5. En formulario, si `ForceOriginalPlan` o `IsOriginalPlan`, forzar `Status = Planned` y `Mode = Planned`.
6. No mostrar acción ejecutar directamente sobre P.O. si el flujo funcional implica crear labor realizada vinculada.

### Backend

1. En `UpdateLabor`, si `labor.IsOriginalPlan == true`, bloquear edición salvo permiso.
2. Mantener regla: P.O. solo `Planned`.
3. Endpoint `unpin-original-plan` debe validar permiso real.
4. Registrar auditoría al desanclar.
5. Verificar campaña bloqueada antes de modificar.
6. Evitar que una labor realizada sea marcada como `IsOriginalPlan`.

## Criterios de aceptación

- Las labores P.O. muestran badge `P.O.`.
- No son editables por usuario común.
- Usuario autorizado puede desanclar.
- El desanclado queda auditado.
- Una labor P.O. no puede pasar a Realizada.
- Campaña bloqueada impide modificar/desanclar.
- Comparación plan vs real no se rompe.

## Pruebas manuales

1. Crear labor P.O. desde flujo permitido.
2. Verla en listado con badge `P.O.`.
3. Intentar editar como usuario no autorizado.
4. Confirmar bloqueo.
5. Intentar cambiar a Realizada.
6. Confirmar bloqueo.
7. Desanclar como administrador.
8. Confirmar auditoría.
9. Editar luego de desanclar.
