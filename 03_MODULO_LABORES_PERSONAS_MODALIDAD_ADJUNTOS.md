# Módulo 03 — Labores, Personas, Modalidad y Adjuntos

Bugs incluidos: BUG-01, BUG-10, BUG-11, BUG-12, BUG-13 y BUG-14.

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

Unificar la gestión de personas operativas y asegurar que la modalidad Propio/Contratista se use correctamente al crear labores. Además, persistir Retiro de Insumos y exponer la biblioteca de adjuntos.

## Archivos a revisar

- `src/GestorOT.Client/Components/LaborEditorForm.razor`
- `src/GestorOT.Client/Pages/LaboresSueltas.razor`
- `src/GestorOT.Api/Controllers/LaborsController.cs`
- `src/GestorOT.Domain/Entities/Labor.cs`
- `src/GestorOT.Shared/Dtos/LaborDto.cs`
- `src/GestorOT.Infrastructure/Data/Configurations/LaborConfiguration.cs`
- Pantallas de Directorio ERP / Personal Activo / Personas.
- Controladores y DTOs de contactos/personas.
- Componentes de adjuntos y controlador de archivos.

## BUG-10 y BUG-11 — Personas

### Implementación requerida

1. Renombrar en UI “Directorio ERP” a “Personas”.
2. Eliminar o esconder pantalla redundante “Personal Activo”.
3. Crear una sola pantalla con filtro: “Todas” / “Activas”.
4. El selector operativo debe mostrar solo modalidades: `Propio` y `Contratista`.
5. No mezclar roles de acceso del sistema con modalidad operativa.
6. Revisar navegación lateral y breadcrumbs.

### Aceptación

- Existe una sola pantalla Personas.
- Filtro alterna todas/activas.
- Modalidad no muestra Administrador, Agrónomo ni roles de acceso.
- La nomenclatura es consistente: “Modalidad”, no “Rol”.

## BUG-12 — Editar modalidad

### Implementación requerida

1. Agregar edición inline o modal en pantalla Personas.
2. Guardar modalidad en backend.
3. Actualizar listado sin recarga dura.
4. Validar valores permitidos: Propio/Contratista.
5. Mantener personas activas/inactivas.

### Aceptación

- El usuario cambia modalidad.
- El cambio persiste al recargar.
- La modalidad actualizada se usa en nuevas labores.

## BUG-13 — Precargar modalidad en Labor

### Implementación requerida

1. Asegurar que `ContactDto` o DTO equivalente exponga modalidad.
2. En `LaborEditorForm`, usar `OnSelectedItemChanged` del selector de persona.
3. Al seleccionar persona, setear `_model.IsExternalBilling`:
   - `Propio` => `false`
   - `Contratista` => `true`
4. Permitir modificación manual si el flujo funcional lo permite.
5. Si la persona no tiene modalidad, mostrar advertencia.

### Aceptación

- Seleccionar persona Propio marca Propio.
- Seleccionar persona Contratista marca Contratista.
- Guardar labor conserva modalidad.
- Editar persona cambia precarga en labores futuras.

## BUG-14 — Retiro de Insumos

### Implementación requerida

1. Verificar entidad `Labor` y columna `SupplyWithdrawalNotes`.
2. Verificar `LaborDto`.
3. En `LaborsController.CreateLabor`, asignar `SupplyWithdrawalNotes = dto.SupplyWithdrawalNotes`.
4. En `UpdateLabor`, actualizar el campo.
5. En `MapToDto`, devolver el campo.
6. En `LaborEditorForm`, asegurar binding correcto.
7. Verificar serializer context si aplica.

### Aceptación

- Completar Retiro de Insumos al crear labor.
- Guardar.
- Reabrir labor.
- El texto sigue visible.
- Editar texto y reabrir conserva el cambio.
- Funciona con labor planeada y, si corresponde, con realizada.

## BUG-01 — Biblioteca de archivos

### Implementación requerida

1. Crear ruta UI “Biblioteca de Archivos”.
2. Listar archivos conservados en biblioteca.
3. Permitir previsualizar imágenes y PDF.
4. Permitir descargar.
5. Permitir asociar archivo existente a labor u OT.
6. Respetar permisos/tenant.
7. Si se cancela una labor con archivos nuevos, mantener pregunta existente: eliminar o conservar.

### Aceptación

- Archivo subido y conservado aparece en Biblioteca.
- Se puede previsualizar imagen/PDF.
- Se puede descargar.
- Eliminar labor no deja archivos inaccesibles si se conservaron.

## Pruebas de regresión

1. Crear persona Propio.
2. Crear persona Contratista.
3. Editar modalidad.
4. Crear labor seleccionando cada persona.
5. Verificar modalidad precargada.
6. Completar Retiro de Insumos.
7. Guardar, reabrir y editar labor.
8. Subir adjunto, cancelar y conservar.
9. Abrir Biblioteca y descargar archivo.
