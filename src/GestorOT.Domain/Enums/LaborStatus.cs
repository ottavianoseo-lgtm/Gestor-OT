namespace GestorOT.Domain.Enums;

public enum LaborStatus
{
    Planned = 0,           // Labor creada, pendiente de ejecución
    AwaitingValidation = 1, // Enviada al ejecutor para confirmación
    Validated = 2,         // Confirmada por el ejecutor
    Realized = 3,          // Ejecutada (puede ser sin planificación)
    Pending = 4            // Pendiente de asignación o acción
}
