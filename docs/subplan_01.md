Sub-Plan 01 — Correcciones Críticas de UI
Sprint 1  ·  Semana 1  ·  Prioridad: ALTA

Este sub-plan contiene todas las correcciones que impactan la usabilidad diaria del sistema. Son cambios de UI y configuración que no requieren migraciones de base de datos. El agente debe completarlas antes de comenzar cualquier otro sub-plan para garantizar que el entorno visual esté estable.

#	Tarea	Archivo Principal	Est.
1	Fix: Menú sidebar — _selectedKeys reactivos	MainLayout.razor	2h
2	Fix: Textos negros → gris con variables CSS	app.css + *.razor	4h
3	Quitar campo Campaña del modal de OT	OrdenesTrabajos.razor	1h
4	WorkOrderId no obligatorio en LaborEditorForm	LaborEditorForm.razor	1h
5	Endpoint activities correcto	LaborEditorForm.razor	0.5h


Tarea 1 — Bug: Menú Sidebar (MainLayout.razor)
Causa Raíz
La variable _activeKey = "1" se declara en MainLayout.razor pero NUNCA se actualiza al cambiar de ruta. Los múltiples bloques <Menu> usan DefaultSelectedKeys (propiedad de inicialización, no reactiva), causando que todos los items queden marcados como seleccionados.
Líneas afectadas: 50, 65, 92, 119, 134, 157 de MainLayout.razor.

Solución Paso a Paso
•	Paso 1 — Unificar todos los bloques <Menu> en UNO SOLO con MenuItemGroup agrupando las secciones.
•	Paso 2 — Cambiar DefaultSelectedKeys → @bind-SelectedKeys con variable reactiva.
•	Paso 3 — Implementar IDisposable y suscribirse a NavigationManager.LocationChanged.
•	Paso 4 — Crear método UpdateSelectedKey(string uri) con switch por path.
•	Paso 5 — Desuscribirse en Dispose() para evitar memory leaks.

Código de Referencia
// Campo
private string[] _selectedKeys = Array.Empty<string>();

// Inicialización
protected override void OnInitialized() {
    Nav.LocationChanged += OnLocationChanged;
    UpdateSelectedKey(Nav.Uri);
}

// Handler de cambio de ruta
private void OnLocationChanged(object? s, LocationChangedEventArgs e) {
    UpdateSelectedKey(e.Location); StateHasChanged();
}

// Mapeo de rutas
private void UpdateSelectedKey(string uri) {
    var path = new Uri(uri).AbsolutePath;
    _selectedKeys = path switch {
        "/" or "" => new[] { "1" },
        var p when p.StartsWith("/planner") => new[] { "10" },
        var p when p.StartsWith("/workorders") => new[] { "4" },
        _ => Array.Empty<string>()
    };
}

// Dispose
public void Dispose() => Nav.LocationChanged -= OnLocationChanged;

⚠ Versión de AntDesign Blazor
Verificar que la versión instalada sea >= 0.18.x para que @bind-SelectedKeys funcione correctamente con múltiples MenuItems. En versiones anteriores, hacer el binding manualmente via SelectedKeysChanged.


Tarea 2 — Fix: Textos Negros (app.css + *.razor)
Contexto
El sistema usa tema oscuro (fondo #1E1E2E). Textos con color:#000 son ilegibles. Los componentes de AntDesign generan texto negro por defecto en modales con fondo claro.

Cambios en app.css
•	Agregar variables CSS globales en :root
•	Sobreescribir componentes de AntDesign: DatePicker, InputNumber, Select, Calendar

:root {
  --text-primary:   #E0E0E0;
  --text-secondary: #A0A0A0;
  --text-muted:     #666680;
}

/* AntDesign overrides */
.ant-picker, .ant-picker input,
.ant-select-selection-item,
.ant-input-number-input {
  color: var(--text-primary) !important;
}

/* Calendarios internos del DatePicker */
.ant-picker-calendar-date-value,
.ant-picker-cell-inner {
  color: var(--text-primary) !important;
}

Cambios en archivos .razor
•	Buscar y reemplazar todos los estilos inline: color: black / color: #000 / color: #000000
•	Reemplazar por: color: var(--text-primary) o eliminar el atributo si hereda del padre


Tarea 3 — Quitar Campo 'Campaña' del Modal de OT
Archivo: OrdenesTrabajos.razor
•	Eliminar el FormItem Label='Campaña' completo (líneas 125-129)
•	En OpenCreateModal() → agregar: _formModel.CampaignId = CampaignState.CurrentCampaign?.Id ?? Guid.Empty;
•	Verificar que CampaignState.CurrentCampaign no sea null al crear OT; mostrar alerta si no hay campaña activa
•	En OpenEditModal() → mantener la asignación existente desde wo.CampaignId


Tarea 4 — WorkOrderId No Obligatorio
Archivo: LaborEditorForm.razor
•	Verificar que en HandleSubmit no haya validación que bloquee si WorkOrderId es null
•	El <Select> de Orden de Trabajo ya tiene AllowClear y Placeholder='Sin asignar' — solo confirmar que funcione
Archivo: GestorOT.Shared/Dtos/LaborDto.cs
•	Confirmar que WorkOrderId sea Guid? (nullable)
•	Remover cualquier anotación [Required] sobre WorkOrderId


Tarea 5 — Endpoint de Activities Correcto
Archivo: LaborEditorForm.razor
•	Verificar que la llamada al catálogo de actividades use: api/catalogs/activities
•	NO debe filtrar actividades por OT — es un catálogo global
•	Si está incorrecto, corregir la URL del HttpClient en el método que carga el Select de tipos de actividad
