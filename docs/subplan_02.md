Sub-Plan 02 — Domain & Infraestructura
Sprint 2 — Inicio  ·  Semana 2  ·  Prioridad: ALTA

Este sub-plan crea las bases estructurales que todos los cambios de backend y frontend de labores necesitan. Debe ejecutarse PRIMERO en el Sprint 2, antes de los sub-planes 03 y 04. Incluye cambios en la capa Domain, Shared y la migración de EF Core.

⚠ Dependencia crítica
Los Sub-Planes 03 y 04 no pueden iniciarse hasta que este sub-plan esté completado y la migración aplicada en la base de datos.

#	Tarea	Archivo	Est.
1	Crear enum LaborStatus	LaborStatus.cs (NUEVO)	0.5h
2	Modificar entidad Labor (Priority, SupplyWithdrawalNotes, Status→enum)	Labor.cs	1h
3	Actualizar LaborConfiguration.cs	LaborConfiguration.cs	0.5h
4	Actualizar LaborDto con nuevos campos	LaborDto.cs	0.5h
5	Crear migración EF Core	Migrations/	3h
6	Agregar WeatherLog.WindDirection	WeatherLog model	0.5h


Tarea 1 — Crear Enum LaborStatus
Archivo: src/GestorOT.Domain/Enums/LaborStatus.cs (NUEVO)
namespace GestorOT.Domain.Enums;

public enum LaborStatus
{
    Planned = 0,           // Labor creada, pendiente de ejecución
    AwaitingValidation = 1, // Enviada al ejecutor para confirmación
    Validated = 2,         // Confirmada por el ejecutor
    Realized = 3           // Ejecutada (puede ser sin planificación)
}


Tarea 2 — Modificar Entidad Labor
Archivo: src/GestorOT.Domain/Entities/Labor.cs
Agregar los siguientes campos a la clase Labor:
•	public int Priority { get; set; } = 0; — para ordenamiento de labores sueltas
•	public string? SupplyWithdrawalNotes { get; set; } — instrucciones de retiro de insumos (texto libre)
•	Cambiar Status de string a LaborStatus enum (ver migración de datos en Tarea 5)
El campo WeatherLogJson ya existe. No modificar su tipo — solo se agrega WindDirection en el modelo interno.


Tarea 3 — Actualizar LaborConfiguration
Archivo: src/GestorOT.Infrastructure/Data/Configurations/LaborConfiguration.cs
•	Mapear Priority: builder.Property(l => l.Priority).HasDefaultValue(0);
•	Mapear SupplyWithdrawalNotes: builder.Property(l => l.SupplyWithdrawalNotes).IsRequired(false);
•	Mapear Status como enum: builder.Property(l => l.Status).HasConversion<string>(); (guardar como string para legibilidad)


Tarea 4 — Actualizar LaborDto
Archivo: src/GestorOT.Shared/Dtos/LaborDto.cs
•	Agregar: public int Priority { get; set; }
•	Agregar: public string? SupplyWithdrawalNotes { get; set; }
•	Cambiar el tipo de Status de string a LaborStatus (o mantener string con el nombre del enum para compatibilidad con el cliente)
•	Verificar que WorkOrderId sea Guid? (nullable) — ya debe serlo según el código fuente


Tarea 5 — Migración EF Core
Nombre: AddLaborPriorityAndWithdrawal
Crear la migración con: dotnet ef migrations add AddLaborPriorityAndWithdrawal

SQL equivalente que debe generar la migración:
ALTER TABLE "Labors" ADD COLUMN "Priority" INT NOT NULL DEFAULT 0;
ALTER TABLE "Labors" ADD COLUMN "SupplyWithdrawalNotes" TEXT NULL;

-- Migración de datos: Status string → LaborStatus enum (guardado como string)
-- PASO 1: verificar que no haya valores inesperados
SELECT DISTINCT "Status" FROM "Labors";

-- PASO 2: normalizar valores existentes al nuevo enum
UPDATE "Labors" SET "Status" = 'Planned'  WHERE "Status" = 'Planned';
UPDATE "Labors" SET "Status" = 'Realized' WHERE "Status" IN ('Realized', 'Pending');

⚠ Riesgo Medio — Migración de Status
Verificar que no haya registros con valores de Status fuera del set conocido ('Planned', 'Realized', 'Pending') antes de aplicar la constraint. Ejecutar el SELECT de verificación primero. Si hay valores inesperados, mapearlos manualmente antes de continuar.


Tarea 6 — Agregar WindDirection al Modelo WeatherLog
Localizar la clase WeatherLog
Buscar WeatherLog en GestorOT.Shared o en los archivos del Client (puede estar como clase interna).
•	Agregar: public string? WindDirection { get; set; }
•	Valores permitidos: N, NE, E, SE, S, SO, O, NO (rosa de los vientos)
Esta clase se serializa a JSON y se guarda en Labor.WeatherLogJson. No requiere migración de DB ya que es un campo JSON string.
