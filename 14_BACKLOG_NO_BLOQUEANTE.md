# Backlog no bloqueante

## Objetivo

Separar mejoras útiles de los bugs críticos para evitar que DeepSeek V4 Pro mezcle alcance.

Estas tareas no deben entrar en los sprints principales salvo que el usuario las priorice.

## Mejoras UX

1. Mejorar diseño visual general de tablas.
2. Agregar filtros avanzados por campaña, estado, responsable y cultivo.
3. Agregar búsqueda global.
4. Agregar modo compacto para pantallas con muchas columnas.
5. Exportación a Excel/PDF de listados.
6. Auditoría visual de cambios.
7. Dashboard de desvíos plan vs real.
8. Indicadores de avance por campaña.

## Mejoras técnicas

1. Reemplazar almacenamiento de archivos en DB por storage externo.
2. Implementar caché de catálogos.
3. Optimizar queries con `AsSplitQuery` donde haya includes pesados.
4. Agregar paginación server-side para tablas grandes.
5. Agregar observabilidad con logs estructurados.
6. Agregar tracing por request.
7. Normalizar estados con enums fuertes donde sea posible.
8. Agregar contratos OpenAPI más estrictos.

## Mejoras de dominio

1. Modelo de aprobación por rol.
2. Workflow configurable de OT.
3. Auditoría agronómica de desvíos.
4. Integración contable con Gestor Max.
5. Comparativa por campaña/cultivo/lote.
6. Reporte de insumos planificados vs reales.
7. Mapa de labores por estado.
8. Validación de stock antes de aprobación.

## Regla

No implementar nada de este backlog hasta que estén cerrados:

- Campañas.
- Lotes/CampaignLot.
- OT.
- Labores.
- Rotaciones.
- Planeamiento Original.
- Estrategias.
- Adjuntos.
- QA final.
