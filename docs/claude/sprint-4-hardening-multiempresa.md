# Sprint 4 — Hardening, Multi-Empresa y Testing
**GestorMax · Gestor OT** | Semanas 7–8 | Prioridad: ALTA (pre-release)

---

## Objetivo

Consolidar la calidad y seguridad del sistema antes del release definitivo a producción: definir e implementar la política de datos multi-empresa, agregar tests de regresión para los bugs críticos corregidos, y auditar los endpoints de datos sensibles.

> **Prerequisito:** Sprints 1, 2 y 3 completados.

---

## Tareas

### Tarea A — Definir e implementar política Multi-Empresa (Multi-Tenant)

**Archivos clave:** `TenantService.cs`, `BUSINESS_LOGIC_MASTER.md`, todos los Controllers  
**Esfuerzo estimado:** 3 días

**Contexto:**  
Existe una pregunta abierta sobre si múltiples empresas (Tenants) dentro de una misma instalación comparten entidades base como Personas, Centros de Costo y entidades de Directorio ERP. La decisión tiene impacto en:

- Filtros en todos los endpoints de lectura (¿datos del grupo o solo del tenant activo?).
- Seed data y migrations (¿entidades base son globales o por tenant?).
- Seguridad: asegurar que un tenant no pueda acceder a datos de otro.

**Tareas concretas:**

1. **Reunión de definición de negocio** (no técnica): acordar formalmente si el modelo es:
   - **Modelo A — Aislado:** cada tenant tiene sus propias entidades base. No hay datos compartidos entre empresas.
   - **Modelo B — Grupo:** las entidades base (Personas, Físicos, Centros de Costo) son compartidas dentro de un `GroupId`. Cada tenant pertenece a un grupo.

2. Documentar la decisión en `BUSINESS_LOGIC_MASTER.md` con ejemplos concretos de qué es compartido y qué es exclusivo por tenant.

3. Si se elige **Modelo B (Grupo)**:
   - Agregar campo `GroupId` a la entidad `Tenant`.
   - Migration de base de datos para agregar la columna y rellenar datos existentes.
   - Actualizar `TenantService.cs` para exponer `CurrentGroupId`.
   - Actualizar las queries de entidades base en los Controllers para filtrar por `GroupId` en lugar de `TenantId`.

4. Independientemente del modelo elegido: **auditar todos los Controllers** para verificar que ningún endpoint retorna datos de otro tenant/grupo. Revisar especialmente:
   - Endpoints que no filtran por `TenantId` explícitamente.
   - Endpoints con parámetros de Id en la URL (verificar que el recurso pertenece al tenant activo antes de retornarlo).

5. Agregar tests de autorización: un usuario del Tenant A no debe poder leer ni modificar datos del Tenant B, incluso si conoce los IDs.

---

### Tarea B — Tests de regresión para bugs críticos #1-4

**Archivos clave:** `*.Tests.csproj` (nuevo proyecto), componentes Blazor  
**Esfuerzo estimado:** 2 días

**Contexto:**  
Los bugs #1-4 demuestran ausencia de tests de integración para el flujo de creación/edición de lotes. Sin tests, una refactorización futura puede reintroducir silenciosamente los mismos problemas.

**Tareas concretas:**

1. Crear (o completar) el proyecto de tests `GestorOT.Tests.csproj` con dependencias:
   - `xUnit` para tests de servicios y controllers.
   - `bUnit` para tests de componentes Blazor.
   - `Moq` o equivalente para mocking de servicios.

2. Escribir tests de regresión para cada bug crítico:

   **Bug #4 (CadastralArea):**
   ```
   Test: SaveLot_Should_Include_CadastralArea_In_DTO
   - Dado: un formulario de lote con CadastralArea = 5.75
   - Cuando: se llama a SaveLot()
   - Entonces: el LotDto enviado al API contiene CadastralArea = 5.75
   ```

   **Bug #1-3 (Estado de campaña):**
   ```
   Test: SaveLot_Should_Not_Overwrite_LotName_With_CampaignName
   - Dado: un formulario con Name = "Lote Norte" y campaña activa "Campaña 2026"
   - Cuando: CampaignState.OnChange dispara durante el await de SaveLot()
   - Entonces: el LotDto enviado al API contiene Name = "Lote Norte", no "Campaña 2026"
   
   Test: SaveLot_Should_Not_Overwrite_CadastralArea_With_GisArea
   - Dado: un formulario con CadastralArea = 10.0 y área GIS calculada = 9.3
   - Cuando: se llama a SaveLot()
   - Entonces: el LotDto enviado al API contiene CadastralArea = 10.0
   ```

   **Bug #9 (Fechas rotación):**
   ```
   Test: CreateRotation_Should_Fail_When_StartDate_GreaterThan_EndDate
   - Dado: dto.StartDate = 2026-06-01, dto.EndDate = 2026-01-01
   - Cuando: se llama a RotationService.CreateRotationAsync(dto)
   - Entonces: retorna error con mensaje descriptivo, no persiste la rotación
   ```

3. Integrar los tests en el pipeline de CI para que fallen automáticamente si se regresan.

---

### Tarea C — Auditoría de campos omitidos en DTOs (patrón bug #4)

**Archivos clave:** `LotsController.cs`, `LotDto.cs` y todos los DTOs de POST/PUT  
**Esfuerzo estimado:** 1 día

**Contexto:**  
El bug #4 (CadastralArea omitida en el constructor del LotDto) puede replicarse en otros DTOs del sistema que usen constructores posicionales con muchos parámetros.

**Tareas concretas:**

1. Listar todos los records/clases DTO que se usan en operaciones POST y PUT del sistema.
2. Para cada uno, verificar que todos los campos del formulario correspondiente estén incluidos en la construcción del DTO antes de enviarlo al API.
3. **Refactoring preventivo recomendado:** reemplazar los records con constructores posicionales por objetos con inicializadores nombrados:
   ```csharp
   // Antes (posicional — propenso a omisiones):
   new LotDto(id, fieldId, name, status, wkt)
   
   // Después (nombrado — seguro):
   new LotDto { Id = id, FieldId = fieldId, Name = name, 
                 Status = status, WktGeometry = wkt, 
                 CadastralArea = _formModel.CadastralArea }
   ```
4. Verificar que los endpoints `PUT` (parciales) no sobreescriban silenciosamente `CadastralArea` o `WktGeometry` cuando no están incluidos en el body de la request.

---

### Tarea D — Documentación del módulo GIS

**Archivos clave:** `gisDoc.md`  
**Esfuerzo estimado:** 1 día (puede hacerse en paralelo)

**Contexto:**  
El módulo GIS es uno de los más complejos del sistema. La ausencia de documentación del flujo completo es un riesgo de onboarding y mantenimiento.

**Tareas concretas:**

1. Completar `gisDoc.md` con diagramas de secuencia para los tres flujos principales:
   - **Flujo 1:** Crear lote → Dibujar geometría → Asignar.
   - **Flujo 2:** Importar geometría desde archivo externo.
   - **Flujo 3:** Editar geometría existente.

2. Documentar la interacción entre Blazor y el código JS de Leaflet (`map.js`): qué funciones JS llama Blazor y viceversa.

3. Documentar las convenciones de WKT usadas en el sistema y cómo se almacenan en Postgres.

---

## Criterios de aceptación del Sprint 4

- [ ] `BUSINESS_LOGIC_MASTER.md` documenta formalmente la política multi-empresa elegida.
- [ ] Todos los Controllers auditados filtran correctamente por `TenantId` (o `GroupId` si aplica).
- [ ] Test de autorización: usuario de Tenant A no puede leer datos de Tenant B.
- [ ] Tests de regresión para bugs #1-4 escritos y pasando en CI.
- [ ] Los tests están integrados en el pipeline y bloquean el merge si fallan.
- [ ] Auditoría de DTOs completada: no hay campos de formulario omitidos en constructores posicionales.
- [ ] Endpoints PUT verificados: no sobreescriben campos no incluidos en el body.
- [ ] `gisDoc.md` completo con los 3 flujos GIS documentados.

---

## Notas para el agente

- La Tarea A (política multi-empresa) **requiere decisión del equipo de negocio** antes de poder implementarse. Si la decisión no está disponible al inicio del sprint, comenzar por las Tareas B, C y D en paralelo.
- La Tarea D (documentación GIS) puede asignarse a un desarrollador junior o hacerse como pair programming para acelerar el onboarding.
- Al finalizar este sprint, el sistema está en condiciones de release a producción.
