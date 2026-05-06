# Módulo 06 — QA, regresión y checklist final

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

Validar que las correcciones no resuelvan un bug rompiendo reglas centrales de GestorOT.

## Comandos sugeridos

El agente debe ajustar los comandos al repo real luego de revisar la solución.

```bash
dotnet restore
dotnet build
dotnet test
```

Si no hay tests existentes, documentar la ausencia y crear pruebas mínimas donde sea razonable.

## Matriz de regresión por contexto

### Campañas

- Sin campaña seleccionada: no permitir crear OT/labor.
- Campaña activa: permitir planificar y ejecutar.
- Campaña bloqueada: permitir consultar, no modificar.
- Campaña futura cargada: permitir seleccionar manualmente, no forzar selección.
- Crear campaña nueva: selector refresca sin spinner infinito.

### Lotes y superficies

- Lote con superficie manual sin GIS.
- Lote con GIS sin superficie manual.
- Lote con ambas.
- Editar superficie manual.
- Editar geometría.
- Asignar campaña ya usada: bloquear duplicado real.
- Asignar campaña no usada: permitir.
- Mostrar superficies con 2 decimales.

### Labores

- Crear labor suelta planeada.
- Crear labor suelta realizada.
- Crear labor con persona Propio.
- Crear labor con persona Contratista.
- Guardar Retiro de Insumos.
- Adjuntar archivo.
- Cancelar labor con archivo nuevo y elegir eliminar.
- Cancelar labor con archivo nuevo y elegir conservar.
- Ver biblioteca.

### OTs

- Crear OT con estado dinámico default.
- Crear OT con estado dinámico no default.
- Editar estado.
- Estado no editable bloquea edición.
- Campaña bloqueada bloquea creación.
- Crear OT desde labores seleccionadas.

### Estrategias

- Crear estrategia con 1 labor.
- Crear estrategia con 3 labores.
- Ver separadores entre labores.
- Eliminar labor intermedia.
- Guardar y reabrir.
- Aplicar estrategia a lotes filtrando por campo.
- Confirmar bulk creation.
- Ver labores creadas filtradas por campaña.

### Planeamiento Original

- Crear labor P.O.
- Ver badge P.O.
- Bloquear edición para usuario común.
- Desanclar con permiso.
- Registrar auditoría.

## Checklist de aceptación por bug

| Bug | Validación mínima |
|---|---|
| BUG-01 | Biblioteca lista, previsualiza y descarga archivos. |
| BUG-02 | Labores Sueltas filtra por campaña activa y tiene opción Ver todas. |
| BUG-03 | Crear campaña refresca selector sin Ctrl+F5. |
| BUG-04 | Botón GIS permite editar y guardar geometría. |
| BUG-05 | Superficies visibles con 2 decimales. |
| BUG-06 | Campaña 27/28 se asigna si no existe duplicado activo. |
| BUG-07 | Separador se muestra entre labores, no en la última. |
| BUG-08 | Editar hectáreas persiste. |
| BUG-09 | Campo y lote se filtran por campaña activa. |
| BUG-10 | Personas usa solo Propio/Contratista. |
| BUG-11 | Hay una sola pantalla Personas. |
| BUG-12 | Modalidad editable y persistente. |
| BUG-13 | Modalidad se precarga al elegir persona. |
| BUG-14 | Retiro de Insumos se guarda y reabre. |
| BUG-15 | Estados OT salen de configuración dinámica. |
| BUG-16 | Se puede crear OT. |
| BUG-17 | P.O. solo lectura y nomenclatura consistente. |
| BUG-18 | Estrategia mantiene orden de labores. |
| BUG-19 | Wizard de estrategia filtra por campo. |
| BUG-20 | Se elimina labor individual en estrategia. |
| BUG-21 | Se crean labores desde estrategia sin error crítico. |

## Reporte final requerido

El agente debe entregar:

1. Resumen de cambios por bug.
2. Archivos modificados.
3. Documentación Context7 consultada.
4. Tests automatizados ejecutados.
5. Pruebas manuales ejecutadas.
6. Bugs no resueltos o parcialmente resueltos.
7. Riesgos pendientes.
8. Recomendación de PR.
