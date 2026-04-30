using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using AntDesign;
using GestorOT.Shared.Dtos;
using System.Net.Http.Json;
using GestorOT.Shared;

namespace GestorOT.Client.Pages
{
    public class WorkOrderDetailFinalBase : ComponentBase
    {
        [Parameter] public string WorkOrderIdParam { get; set; } = string.Empty;
        public Guid WorkOrderId => Guid.TryParse(WorkOrderIdParam, out var g) ? g : Guid.Empty;

        [Inject] protected HttpClient _http { get; set; } = default!;
        [Inject] protected IMessageService _message { get; set; } = default!;
        [Inject] protected IJSRuntime _js { get; set; } = default!;
        [Inject] protected NavigationManager _navigation { get; set; } = default!;
        [Inject] protected GestorOT.Client.Services.CampaignState _campaignState { get; set; } = default!;
        [Inject] protected ModalService _modalService { get; set; } = default!;

        protected WorkOrderDetailDto? _order;
        protected bool _loading = true;
        protected List<WorkOrderStatusDto> _availableStatuses = new();
        protected List<ContactDto> _availableContacts = new();
        protected string _viewMode = "Planned";
        protected bool _savingGlobal;
        protected bool _showLaborModal;
        protected Guid _editingLaborId;
        protected string? _pendingInitialStatus;
        protected List<Guid> _selectedLabors = new();
        protected ITable? _laborTable;
        protected bool _showValidationModal;
        protected string _validationUrl = "";
        protected bool _showUnassignedWarning;
        protected int _unassignedLaborsCount;

        protected override async Task OnInitializedAsync() => await LoadData();

        protected void ToggleLaborSelection(Guid id, bool selected) {
            if (selected) _selectedLabors.Add(id);
            else _selectedLabors.Remove(id);
        }

        protected async Task SubmitSingleLabor(Guid laborId) {
            try {
                var response = await _http.PostAsync($"api/labors/{laborId}/submit-for-validation", null);
                if (response.IsSuccessStatusCode) {
                    var result = await response.Content.ReadFromJsonAsync<LaborValidationResponse>(AppJsonSerializerContext.Default.LaborValidationResponse);
                    if (result != null) {
                        _validationUrl = result.Url; _showValidationModal = true;
                        await _js.InvokeVoidAsync("utilsInterop.copyToClipboard", _validationUrl);
                        _message.Success("Link de validación copiado al portapapeles."); await LoadData();
                    }
                }
            } catch (Exception ex) { _message.Error(ex.Message); }
        }

        protected async Task SubmitSelectedLabors() {
            if (!_selectedLabors.Any()) return;
            try {
                var request = new GenerateShareLinkRequest { LaborIds = _selectedLabors.ToList(), ExpiryDays = 7 };
                var response = await _http.PostAsJsonAsync($"api/share/generate/{WorkOrderId}", request);
                if (response.IsSuccessStatusCode) {
                    var result = await response.Content.ReadFromJsonAsync<ShareLinkDto>(AppJsonSerializerContext.Default.ShareLinkDto);
                    if (result != null) {
                        _validationUrl = result.Url; _showValidationModal = true; _selectedLabors.Clear();
                        await _js.InvokeVoidAsync("utilsInterop.copyToClipboard", _validationUrl);
                        _message.Success("Link de validación masiva copiado."); await LoadData();
                    }
                }
            } catch (Exception ex) { _message.Error(ex.Message); }
        }

        protected async Task LoadData() {
            _loading = true;
            try {
                _order = await _http.GetFromJsonAsync<WorkOrderDetailDto>($"api/workorders/{WorkOrderId}");
                _availableStatuses = await _http.GetFromJsonAsync<List<WorkOrderStatusDto>>("api/workorderstatuses") ?? new();
                _availableContacts = await _http.GetFromJsonAsync<List<ContactDto>>("api/catalogs/contacts") ?? new();
            } catch (Exception ex) { _message.Error($"Error: {ex.Message}"); }
            finally { _loading = false; }
        }

        protected void BackToList() => _navigation.NavigateTo("/workorders");

        protected async Task ConsolidateSupplies() {
            try {
                var response = await _http.PostAsync($"api/workorders/{WorkOrderId}/consolidate-supplies", null);
                if (response.IsSuccessStatusCode) { _message.Success("Insumos consolidados."); await LoadData(); }
                else { var body = await response.Content.ReadAsStringAsync(); _message.Error($"Error al consolidar insumos: {body}"); }
            } catch (Exception ex) { _message.Error($"Error al consolidar insumos: {ex.Message}"); }
        }

        protected async Task SaveAllChanges() {
            if (_order == null) return;
            _savingGlobal = true;
            try {
                var dto = new WorkOrderDto(_order.Id, _order.FieldId, _order.Description, _order.Status, _order.AssignedTo, _order.DueDate, null, _order.OTNumber, _order.PlannedDate, _order.ExpirationDate, _order.StockReserved, _order.ContractorId, _order.ContactId, _order.CampaignId, _order.Name, _order.AcceptsMultiplePeople, _order.AcceptsMultipleDates, _order.IsLocked);
                var resp = await _http.PutAsJsonAsync($"api/workorders/{WorkOrderId}", dto);
                if (resp.IsSuccessStatusCode) { _message.Success("Orden de Trabajo actualizada correctamente."); await LoadData(); }
                else { var body = await resp.Content.ReadAsStringAsync(); _message.Error($"Error al guardar cambios: {body}"); }
            } catch (Exception ex) { _message.Error($"Error al guardar cambios: {ex.Message}"); }
            finally { _savingGlobal = false; }
        }

        protected async Task OnAddLaborClicked() {
            try {
                _unassignedLaborsCount = await _http.GetFromJsonAsync<int>("api/labors/unassigned/count");
                if (_unassignedLaborsCount > 0) {
                    _showUnassignedWarning = true;
                }
                else
                {
                    OpenLaborModal();
                }
                StateHasChanged();
            } catch (Exception ex) { 
                Console.WriteLine($"Error checking unassigned labors: {ex.Message}");
                OpenLaborModal();
                StateHasChanged();
            }
        }

        protected void OpenLaborModal(Guid? laborId = null, string? initialStatus = null) { 
            _editingLaborId = laborId ?? Guid.Empty; 
            _pendingInitialStatus = initialStatus; 
            _showLaborModal = true; 
            StateHasChanged();
        }

        protected async Task DeleteLabor(Guid id) {
            try {
                var response = await _http.DeleteAsync($"api/labors/{id}");
                if (response.IsSuccessStatusCode) { _message.Success("Labor eliminada."); await LoadData(); }
                else { var body = await response.Content.ReadAsStringAsync(); _message.Error($"Error al eliminar labor: {body}"); }
            } catch (Exception ex) { _message.Error($"Error al eliminar labor: {ex.Message}"); }
        }

        protected async Task OnLaborSaved() { _showLaborModal = false; await LoadData(); }

        protected void ReviewUnassignedLabors()
        {
            _showUnassignedWarning = false;
            _navigation.NavigateTo($"/labores-sueltas?targetOT={WorkOrderId}");
        }

        protected void CreateNewLabor()
        {
            _showUnassignedWarning = false;
            OpenLaborModal();
        }

        protected async Task ExportHtmlInteractivo() {
            try {
                var request = new GenerateShareLinkRequest { LaborIds = _selectedLabors.Any() ? _selectedLabors.ToList() : null, ExpiryDays = 7 };
                var response = await _http.PostAsJsonAsync($"api/share/generate/{WorkOrderId}", request);
                if (response.IsSuccessStatusCode) {
                    var result = await response.Content.ReadFromJsonAsync<ShareLinkDto>(AppJsonSerializerContext.Default.ShareLinkDto);
                    if (result != null) { await _js.InvokeVoidAsync("utilsInterop.copyToClipboard", result.Url); _message.Success("Link de OT compartida copiado al portapapeles."); }
                }
            } catch (Exception ex) { _message.Error($"Error: {ex.Message}"); }
        }

        protected string StatusTagStyle(string colorHex) => $"background: {colorHex}33; color: #fff; border: 1px solid {colorHex}80; border-radius: 12px; font-size: 11px; font-weight: 600; padding: 2px 10px; text-shadow: 0 1px 2px rgba(0,0,0,0.4);";
        protected string GetRowKey(LaborDto l) => l.Id.ToString();
        protected string StatusBadgeStyle(string colorHex) => $"display: inline-flex; align-items: center; gap: 6px; background: {colorHex}33; color: #fff; border: 1px solid {colorHex}80; border-radius: 12px; font-size: 12px; font-weight: 600; padding: 3px 10px; text-shadow: 0 1px 2px rgba(0,0,0,0.4);";
        protected string GetStatusColor(string status) => status switch { "Realized" => "#52C41A", "Validated" => "#FA8C16", "AwaitingValidation" => "#FAAD14", _ => "#1890FF" };
        protected string GetStatusLabel(string status) => status switch { "Realized" => "Realizada", "Validated" => "Validada", "AwaitingValidation" => "En Validación", _ => "Planeada" };
    }
}
