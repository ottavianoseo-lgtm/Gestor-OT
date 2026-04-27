Reglas de implementación
General

Un cambio a la vez: no mezclar múltiples sub-planes en la misma sesión de trabajo.
No eliminar código sin entender su uso: buscar con grep o referencias antes de borrar una propiedad, método o componente.
No dejar TODO sin resolver salvo que el sub-plan lo indique explícitamente como diferido.
No hardcodear IDs, strings de estado, ni URLs. Usar constantes o los enums ya existentes (LaborStatus, CampaignStatus, etc.).

Backend (.NET / ASP.NET Core)

Todos los endpoints filtran por TenantId via ICurrentTenantService. Nunca hacer queries sin este filtro.
Los cambios de estado que bloquean edición deben chequearse en el controller, no solo en el frontend.
Si un endpoint nuevo necesita autorización específica, verificar con el sistema de roles existente.
Ante un error de negocio usar return Conflict("mensaje") o return BadRequest("mensaje"). No lanzar excepciones no controladas.
Después de agregar un campo a una entidad del Domain:

Actualizar la configuración EF en Infrastructure/Data/Configurations/.
Crear la migración: dotnet ef migrations add NombreDescriptivo -p src/GestorOT.Infrastructure -s src/GestorOT.Api.
Verificar que la migración generada sea correcta antes de aplicarla.



Frontend (Blazor / AntDesign)

El componente LaborEditorForm.razor es el único modal de Labor. Si necesitás abrir un modal de labor desde cualquier página, usá ese componente, no crees uno nuevo inline.
Para confirmaciones que no bloquean (el usuario puede elegir continuar): usar Modal.ConfirmAsync().
Para errores que sí bloquean (validación dura): usar Message.Warning() y return.
Los selectores (<Select>) deben tener Placeholder definido y manejar el estado vacío (Guid.Empty o null).
Al agregar un filtro nuevo en una página, el filtro debe ser una propiedad local y la lista filtrada debe ser una IEnumerable computada (no mutar la lista fuente).
StateHasChanged() solo cuando sea necesario (cambios desde eventos externos o async). No spamear.

DTOs (GestorOT.Shared)

Un DTO nuevo o modificado requiere actualizar el AppJsonSerializerContext si el proyecto usa source generation para JSON.
Los DTOs de respuesta (GET) pueden tener campos calculados (ej: IsLocked). Los DTOs de request (POST/PUT) deben ser lo más simples posible.


Checklist de cierre de cada tarea
Antes de declarar una tarea como completa, verificar todo esto:

 Build limpio: dotnet build sin warnings ni errores en todos los proyectos.
 Migración aplicada (si hubo cambios en entidades): dotnet ef database update.
 Ambos lados actualizados: si cambió un DTO, el Api y el Client están en sintonía.
 No hay referencias rotas: ningún componente Blazor referencia un parámetro o método que ya no existe.
 Casos límite cubiertos: el campo nuevo maneja null, lista vacía, y el estado de carga (_loading).
 Sin console.log ni código de debug abandonado.
 Advertencias de negocio funcionan: probar el camino feliz Y el camino de advertencia (ej: Ha mayor que el lote).
 Multi-tenancy intacto: los nuevos endpoints filtran por tenant.


Cómo manejar bugs vs features

Bug (sub-plan 01_bugs_criticos.md): primero reproducir, luego corregir el mínimo necesario. No refactorizar código que no esté roto mientras arreglás un bug.
Feature: implementar exactamente lo especificado en el sub-plan. Si durante la implementación encontrás algo que parece un bug adicional, registrarlo pero no desviarse.


Cuando algo no está claro
Si el sub-plan no especifica un detalle de implementación (ej: no dice qué columna agregar exactamente), usar el criterio más conservador: el que cambia menos cosas y es más fácil de revertir. No inventar funcionalidad no pedida.
Si hay una contradicción entre el sub-plan y el código existente, el código existente tiene la razón: el sub-plan puede tener información desactualizada. Ajustar la implementación al contexto real del código y documentar la diferencia.

Skills disponibles
Este proyecto tiene skills configuradas en .opencode/skills/:

testsprite-testing: usar después de implementar cualquier feature para verificar que no hay regresiones. Ejecutar especialmente después de cambios en controllers o en LaborEditorForm.
context7-docs: consultar antes de usar APIs de AntDesign Blazor, EF Core, o .NET que no sean 100% conocidas. La documentación de AntDesign Blazor cambia entre versiones.


Orden de trabajo recomendado
Siempre resolver en este orden:

01_bugs_criticos.md — sin esto, las features nuevas se prueban sobre un sistema roto.
02_ot_modal_y_tabla.md y 03_labores_mejoras.md — son el corazón del flujo diario.
04 → 07 — mejoras de UX y consistencia.
08_estrategias_ux.md — complejo, no empezar hasta que los módulos base estén estables.
09_planeamiento_original.md — módulo nuevo, requiere más testing.

No mezclar tareas de distintos sub-planes en la misma sesión.