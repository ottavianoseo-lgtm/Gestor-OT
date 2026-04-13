# 🛡️ ESPECIFICACIONES: PANEL DE ADMINISTRACIÓN Y REGLAS AGRONÓMICAS (.NET 10)

## 1. ARQUITECTURA UX/UI (.NET 10 NATIVO)
El Panel de Administración (`/admin`) debe ser modular y de alto rendimiento.

### 1.1 Renderizado Híbrido
* **Modo:** Utilizar `@rendermode InteractiveAuto`.
* **Beneficio:** Carga inicial instantánea (SSR) y posterior interactividad fluida (WASM).
* **Persistencia:** Implementar el atributo `[PersistentState]` en todos los formularios de configuración y filtros de grillas. Si el admin recarga la página, no debe perder los filtros aplicados.

### 1.2 Componentes de Datos (QuickGrid)
* **Estándar:** Reemplazar las tablas HTML/AntDesign pesadas en el admin por `Microsoft.AspNetCore.Components.QuickGrid` para la gestión de usuarios y logs.
* **Funciones:** Paginación, ordenamiento y filtrado nativo sin librerías JS externas.

---

## 2. SEGURIDAD Y GESTIÓN DE IDENTIDAD (RBAC)

### 2.1 Roles y Permisos
* **Modelo:** Implementar RBAC (Role-Based Access Control) conectado a los Claims del Token JWT de Supabase.
* **Roles Iniciales:** `GlobalAdmin`, `TenantAdmin`, `Agronomist`, `Contractor`.
* **Gestión de Usuarios:** CRUD de usuarios que permite asignar roles y asociar usuarios a múltiples Tenants (Espacios de Trabajo).

### 2.2 Auditoría (Audit Logs)
* **Entidad:** `AuditLog` (Id, UserId, Action, EntityAffected, Timestamp, OldValue, NewValue).
* **Trigger:** Cada acción de escritura crítica (Aprobar OT, Cambiar Stock, Modificar Usuario) debe generar un registro inmutable.
* **Visor:** Grid de solo lectura en el panel admin.

---

## 3. MOTOR AGRONÓMICO (REGLAS DE NEGOCIO)
El sistema debe prevenir errores agronómicos antes de que sucedan.

### 3.1 Reglas de Mezcla (Tank Mix)
* **Entidad:** `TankMixRule` (ProductA_Id, ProductB_Id, Severity, WarningMessage).
* **Lógica:** Al agregar insumos a una Labor, el sistema verifica si existe una regla de incompatibilidad (ej: "No mezclar 2,4-D con Graminicida").
* **UX:** Mostrar alerta "Toast" si se detecta una mezcla prohibida.

### 3.2 Umbrales de Alerta
* **Configuración:** Sliders en el panel admin para definir:
    * `MinStockThreshold`: % para avisar "Stock Bajo".
    * `MaxWindSpeed`: Límite de viento para planificación (integración futura con clima).

---

## 4. CONFIGURACIÓN TÉCNICA (OFFLINE)
Control sobre el comportamiento de la PWA.

### 4.1 Políticas de Sync
* **Panel:** Configuración global para definir:
    * `SyncImagesOverWifiOnly`: Bool.
    * `ForceCacheRefresh`: Botón de pánico para invalidar la caché de los clientes móviles (IndexedDB) cuando cambian datos maestros críticos.
