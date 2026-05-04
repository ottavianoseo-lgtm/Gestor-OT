# Sprint 00 - Baseline, build, tests y diagnóstico técnico

## Objetivo

Antes de corregir lógica funcional, dejar claro el estado real de `main`.

Este sprint no debe arreglar bugs funcionales salvo que el proyecto no compile por un error trivial y documentado.

## Rama sugerida

`fix/s00-baseline-build`

## Alcance

### Incluye

- Compilación completa.
- Tests existentes.
- Verificación de migraciones.
- Revisión rápida de estructura del proyecto.
- Registro de errores actuales.

### No incluye

- Cambios de UX.
- Correcciones funcionales.
- Cambios de modelo.
- Refactors.

## Archivos/proyectos a revisar

- Solución `.sln`
- `src/GestorOT.Api`
- `src/GestorOT.Client`
- `src/GestorOT.Domain`
- `src/GestorOT.Application`
- `src/GestorOT.Infrastructure`
- `src/GestorOT.Shared`
- `src/GestorOT.Tests`
- Migraciones EF Core existentes

## Tareas

1. Actualizar dependencias si el repo lo requiere, sin cambiar versiones arbitrariamente.
2. Ejecutar restore.
3. Ejecutar build.
4. Ejecutar tests.
5. Verificar si hay migraciones pendientes.
6. Levantar la app si el entorno lo permite.
7. Registrar errores actuales en un archivo de diagnóstico.

## Diagnóstico que debe producir el agente

Crear o actualizar un documento interno de diagnóstico, por ejemplo:

`docs/debug/s00_baseline_resultado.md`

Debe contener:

- Commit base de `main`.
- Fecha/hora del análisis.
- Comandos ejecutados.
- Resultado de build.
- Resultado de tests.
- Warnings importantes.
- Errores bloqueantes.
- Migraciones detectadas.
- Observaciones.

## Criterios de aceptación

- Existe un diagnóstico claro.
- Se sabe si `main` compila.
- Se sabe si los tests pasan.
- No se introdujeron cambios funcionales.
- Si se tocó algo, fue mínimo y justificado.

## Prompt corto para el agente

Trabajá solo en baseline. No corrijas bugs funcionales todavía. Compilá, ejecutá tests, revisá migraciones y dejá documentado el estado real de `main`. Si algo no compila, corregí solo errores mínimos indispensables para que el proyecto pueda buildar, documentando cada cambio.
