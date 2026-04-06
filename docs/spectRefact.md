Especificación Técnica: Integración ERP (Gestor Max) y Refactorización Core
Contexto: Refactorización del módulo de Órdenes de Trabajo (OT) y Labores para operar como un cliente inteligente integrado con el ERP "Gestor Max" vía API. Stack: .NET 10, Blazor (InteractiveAuto), EF Core 10, PostgreSQL (Supabase).

1. Refactorización del Modelo de Dominio (Entity Framework Core)

1.1. Entidad WorkOrder (Cabecera)

Acción: Eliminar toda lógica y entidad relacionada con la "Liquidación" (ServiceSettlement y sus flujos de estado)
.
Ajuste:

 Asegurar que la OT contenga estrictamente los datos indispensables para Gestor Max: CampaignId, ContractorId/EmployeeId (identificadores del ERP), PlannedDate, Status y AgreedRate (si aplica contablemente en el futuro).
1.2. Entidades de Catálogo Maestros (Sincronización ERP)

Acción: Las entidades LaborType (Labores) y Employee (Personas) pasan a ser de solo lectura para el usuario y se nutren desde el ERP.
Propiedades a añadir: Ambas entidades deben heredar de una interfaz o incluir la propiedad ExternalErpId (string) para mantener el mapeo exacto con Gestor Max al momento de devolver la información.
1.3. Entidad Labor (Detalle de Ejecución)

Datos Indispensables: Asegurar que contenga CampaignLotId, ActivityType (vinculado a LaborType), EffectiveArea, PlannedDose, y RealizedDose
.
Fechas Planificadas vs. Reales: Modificar la entidad y la UI de Estrategias para que soporten explícitamente EstimatedDate (Fecha estimada de planificación) y ExecutionDate (Fecha real de ejecución).
Unidad de Medida: Actualizar la entidad LaborSupply (Insumos utilizados) para incluir la propiedad UnitOfMeasure (string), la cual debe mapearse e inyectarse automáticamente desde la respuesta de la API de Gestor Max al seleccionar el insumo.
1.4. Trazabilidad de Superficies (Audit)

Acción: Implementar un registro de auditoría estricto para los cambios en las superficies.
Implementación: Crear un Interceptor en EF Core (SaveChangesInterceptor) que escuche modificaciones en la propiedad ProductiveArea de la entidad CampaignLot. Cada actualización debe generar un registro automático en la tabla AuditLogs indicando OldValue, NewValue, UserId y Timestamp
.


--------------------------------------------------------------------------------
2. Lógica de Negocio y Sincronización (Backend .NET 10)

2.1. Sincronización con Gestor Max (Background Service)

Componente: Crear un servicio de sincronización (ej. ErpSyncWorker) que consuma la API de Gestor Max.
Endpoints a consumir:
Catálogo de Labores.
Catálogo de Personas/Personal.
Inventario/Stock: Traer el stock actual de insumos desde el ERP utilizando la nueva característica HybridCache de .NET 10 para servir estos datos rápidamente al frontend sin golpear la base de datos constantemente.
2.2. Flexibilización de Reglas de Stock

Acción: Modificar el validador de insumos al crear OTs/Labores.
Lógica: Permitir la creación y asignación de labores incluso si el stock reportado por Gestor Max es 0 o insuficiente. El sistema debe mostrar un Warning (Toast/Alerta visual) en la interfaz indicando la falta de stock en el ERP, pero no debe lanzar un error bloqueante ni detener la transacción en la base de datos.


--------------------------------------------------------------------------------
3. Arquitectura de Interfaz de Usuario (Blazor PWA)

3.1. Componente Universal de Labores (<LaborEditorForm>)

Acción: Unificar la UI. Crear un componente Blazor reutilizable (LaborEditorForm.razor) que encapsule toda la lógica de creación/edición de una labor (selección de lote, actividad, fechas y grilla de insumos con su unidad de medida).
Implementación:
Este componente debe recibir un parámetro [Parameter] public Guid? WorkOrderId { get; set; }.
Si WorkOrderId es nulo, el componente funciona como el módulo de "Labores Sueltas".

Si recibe un ID, funciona como el formulario de carga de labor dentro de una OT.
Debe garantizar que visualmente y funcionalmente ambos escenarios usen exactamente el mismo código y validaciones.
3.2. Actualización del "Work Planner" (Calendario)

Acción: Refactorizar la vista del WorkScheduler.razor.
Visualización: El calendario debe listar todas las labores de forma individual, independientemente de si pertenecen a una OT o si son "labores sueltas".
Interacción: Al hacer clic en un día o en el botón "Nueva Labor" dentro del Work Planner, se debe invocar el modal/drawer del componente universal <LaborEditorForm> ajustado en el punto 3.1.