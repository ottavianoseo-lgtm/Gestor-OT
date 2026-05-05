# Plan de cierre de bugs - Gestor OT

## Objetivo

Este directorio contiene el plan de desarrollo modular para terminar la rama `fix/bugs-revision-gestor-ot` y dejarla lista para revisión final y merge.

El plan está preparado para trabajar con un agente DeepSeek V4 Pro en sprints chicos, sin refactors masivos y con criterios de aceptación verificables.

## Rama de trabajo sugerida

Continuar desde:

```text
fix/bugs-revision-gestor-ot
```

Si se prefiere aislar el cierre final, crear una rama hija:

```text
fix/cierre-final-bugs-gestor-ot
```

## Reglas generales para el agente

1. No hacer refactor masivo.
2. No cambiar arquitectura global.
3. No mezclar módulos.
4. No avanzar al siguiente módulo si el actual no compila.
5. No eliminar entidades, columnas o migraciones existentes sin justificación explícita.
6. Toda regla crítica debe quedar validada en backend aunque exista validación en Blazor.
7. Toda corrección debe tener al menos un test de regresión real.
8. No dejar tests que solo validen strings o booleanos locales.
9. Mantener mensajes de error claros y orientados al usuario.
10. Documentar comandos ejecutados y resultado.

## Orden recomendado de ejecución

1. `01_BASELINE_COMPILACION_Y_TESTS.md`
2. `02_CAMPANIAS_LOTES_SOLO_LECTURA.md`
3. `03_ORDENES_TRABAJO_ESTADOS.md`
4. `04_LABORES_VALIDACIONES_ADJUNTOS.md`
5. `05_ESTRATEGIAS_WIZARD_FECHAS_ROTACIONES.md`
6. `06_PLANEAMIENTO_ORIGINAL.md`
7. `07_TESTS_REGRESION_QA_FINAL.md`

## Criterio final de aceptación

La rama solo puede considerarse lista si:

- `dotnet restore` termina correctamente.
- `dotnet build` termina sin errores.
- `dotnet test` termina sin errores o con fallas documentadas como preexistentes y no relacionadas.
- Se probaron manualmente los flujos críticos.
- No hay mutaciones permitidas sobre campañas bloqueadas.
- Las estrategias crean labores, no OTs implícitas.
- Las labores usan `CampaignLotId` como contexto operativo.
- Planeamiento Original solo crea labores planificadas.
- Adjuntos funcionan antes y después de guardar una labor.
- Los tests agregados ejercitan controllers, servicios o endpoints reales, no lógica local simulada.

## Formato de entrega exigido al agente

Al terminar cada módulo, el agente debe responder con:

```text
Módulo completado:
Resumen:
Archivos modificados:
Migraciones generadas:
Tests agregados/modificados:
Comandos ejecutados:
Resultado de build:
Resultado de tests:
Pruebas manuales realizadas:
Riesgos pendientes:
Qué NO se tocó:
```
