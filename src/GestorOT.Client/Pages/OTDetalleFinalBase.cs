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
        protected string? _originalStatus;
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
        protected GestorOT.Client.Components.LaborEditorForm? _laborFormRef;

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
                if (_order != null) {
                    _originalStatus = _order.Status;
                    if (_order.Labors != null) {
                        _order.Labors = _order.Labors.OrderBy(l => l.CreatedAt).ToList();
                    }
                }
                _availableStatuses = await _http.GetFromJsonAsync<List<WorkOrderStatusDto>>("api/workorderstatuses") ?? new();
                _availableContacts = await _http.GetFromJsonAsync<List<ContactDto>>("api/catalogs/contacts") ?? new();
            } catch (Exception ex) { _message.Error($"Error: {ex.Message}"); }
            finally { _loading = false; StateHasChanged(); }
        }

        protected void BackToList() => _navigation.NavigateTo("/workorders");

        protected async Task ConsolidateSupplies() {
            if (_order == null) return;
            try {
                // First save current real totals if any were modified
                await _http.PutAsJsonAsync($"api/workorders/{WorkOrderId}/approvals", _order.SupplyApprovals);
                
                var response = await _http.PostAsync($"api/workorders/{WorkOrderId}/consolidate-supplies", null);
                if (response.IsSuccessStatusCode) { _message.Success("Insumos consolidados."); await LoadData(); }
                else { var body = await response.Content.ReadAsStringAsync(); _message.Error($"Error al consolidar insumos: {body}"); }
            } catch (Exception ex) { _message.Error($"Error al consolidar insumos: {ex.Message}"); }
        }

        protected async Task SaveAllChanges() {
            if (_order == null) return;
            _savingGlobal = true;
            try {
                // Save header
                var dto = new WorkOrderDto {
                    Id = _order.Id,
                    FieldId = _order.FieldId,
                    Description = _order.Description,
                    Status = _order.Status,
                    AssignedTo = _order.AssignedTo,
                    DueDate = _order.DueDate,
                    FieldName = _order.FieldName,
                    OTNumber = _order.OTNumber,
                    PlannedDate = _order.PlannedDate,
                    ExpirationDate = _order.ExpirationDate,
                    StockReserved = _order.StockReserved,
                    ContractorId = _order.ContractorId,
                    ContactId = _order.ContactId,
                    CampaignId = _order.CampaignId,
                    Name = _order.Name,
                    AcceptsMultiplePeople = _order.AcceptsMultiplePeople,
                    AcceptsMultipleDates = _order.AcceptsMultipleDates,
                    IsLocked = _order.IsLocked
                };
                var resp = await _http.PutAsJsonAsync($"api/workorders/{WorkOrderId}", dto);
                
                // Save approvals (real totals)
                var respApp = await _http.PutAsJsonAsync($"api/workorders/{WorkOrderId}/approvals", _order.SupplyApprovals);

                if (resp.IsSuccessStatusCode && respApp.IsSuccessStatusCode) { 
                    _message.Success("Orden de Trabajo actualizada correctamente."); 
                    await ConsolidateSupplies();
                    await LoadData(); 
                }
                else { _message.Error("Hubo un error al guardar algunos cambios."); }
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

        protected async Task OpenLaborModal(Guid? laborId = null, string? initialStatus = null) { 
            _editingLaborId = laborId ?? Guid.Empty; 
            _pendingInitialStatus = initialStatus; 
            _showLaborModal = true; 
            if (_laborFormRef != null)
            {
                await _laborFormRef.Reset();
            }
            StateHasChanged();
        }

        protected async Task DeleteLabor(Guid id) {
            try {
                var response = await _http.DeleteAsync($"api/labors/{id}");
                if (response.IsSuccessStatusCode) { _message.Success("Labor eliminada."); await LoadData(); }
                else { var body = await response.Content.ReadAsStringAsync(); _message.Error($"Error al eliminar labor: {body}"); }
            } catch (Exception ex) { _message.Error($"Error al eliminar labor: {ex.Message}"); }
        }

        protected async Task HandleCancelLaborModal()
        {
            if (_laborFormRef != null && _laborFormRef.HasDraftUploads())
            {
                var confirmed = await _js.InvokeAsync<bool>("confirm",
                    "Tenés archivos subidos no guardados. ¿Eliminarlos? (Cancelar = conservar en biblioteca)");
                if (!confirmed)
                    _laborFormRef.DiscardDraftUploads();
                else
                {
                    var draftIds = _laborFormRef.GetDraftUploadIds();
                    if (draftIds.Count > 0)
                        await _http.PostAsJsonAsync("api/files/delete-unlinked", new { FileAssetIds = draftIds });
                    _laborFormRef.DiscardDraftUploads();
                }
            }
            _showLaborModal = false;
        }

        protected async Task OnLaborSaved() { _showLaborModal = false; await ConsolidateSupplies(); }

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

        protected async Task ExportPdf()
        {
            try
            {
                var response = await _http.GetAsync($"api/workorders/{WorkOrderId}/export-pdf");
                if (!response.IsSuccessStatusCode)
                {
                    _message.Error($"Error al generar PDF: {response.StatusCode}");
                    return;
                }

                var bytes = await response.Content.ReadAsByteArrayAsync();
                var fileName = response.Content.Headers.ContentDisposition?.FileName?.Trim('"')
                               ?? $"OT-{WorkOrderId}.pdf";

                await _js.InvokeVoidAsync("utilsInterop.downloadFile", fileName, "application/pdf", bytes);
            }
            catch (Exception ex)
            {
                _message.Error($"Error: {ex.Message}");
            }
        }

        protected string? _filterLaborStatus;
        protected bool _breakdownDrawerVisible;
        protected WorkOrderSupplyApprovalDto? _breakdownSupply;
        protected decimal _breakdownPlannedTotal;
        protected List<SupplyBreakdownRow> _plannedRows = new();
        protected List<SupplyBreakdownRow> _realizedRows = new();

        public record SupplyBreakdownRow(string? LotName, decimal Hectares, decimal Coef, decimal Cantidad, decimal Percent);

        protected void RecalculateProportionalSupplies(Guid supplyId)
        {
            if (_order == null) return;

            var approval = _order.SupplyApprovals.FirstOrDefault(a => a.SupplyId == supplyId);
            if (approval?.RealTotalUsed == null) return;

            var laborsWithSupply = _order.Labors
                .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
                .ToList();

            var totalPlanned = laborsWithSupply
                .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

            if (totalPlanned <= 0) return;

            foreach (var labor in laborsWithSupply)
            {
                foreach (var supply in labor.Supplies.Where(s => s.SupplyId == supplyId))
                {
                    var proportion = supply.PlannedTotal / totalPlanned;
                    supply.CalculatedTotal = approval.RealTotalUsed.Value * proportion;

                    var area = supply.RealHectares ?? labor.Hectares;
                    if (area > 0)
                    {
                        supply.CalculatedDose = supply.CalculatedTotal / area;
                    }
                }
            }
        }

        protected async Task HandleRealTotalChange(WorkOrderSupplyApprovalDto context, decimal? newValue)
        {
            var confirmed = await _js.InvokeAsync<bool>("confirm",
                "Este valor modificará los valores realizados en las labores de esta OT " +
                "de forma proporcional. ¿Estás seguro?");

            if (!confirmed) return;

            context.RealTotalUsed = newValue;

            if (newValue.HasValue)
            {
                RecalculateProportionalSupplies(context.SupplyId);
            }
        }

        protected void ClearOverride(WorkOrderSupplyApprovalDto context)
        {
            context.RealTotalUsed = null;
        }

        protected void OpenSupplyBreakdownDrawer(Guid supplyId)
        {
            _breakdownSupply = _order!.SupplyApprovals.FirstOrDefault(a => a.SupplyId == supplyId);
            if (_breakdownSupply == null) return;

            var laborsWithSupply = _order.Labors
                .Where(l => l.Supplies.Any(s => s.SupplyId == supplyId))
                .ToList();

            _breakdownPlannedTotal = laborsWithSupply
                .Sum(l => l.Supplies.Where(s => s.SupplyId == supplyId).Sum(s => s.PlannedTotal));

            _plannedRows = laborsWithSupply.SelectMany(l => l.Supplies
                .Where(s => s.SupplyId == supplyId)
                .Select(s => new SupplyBreakdownRow(
                    l.LotName,
                    s.PlannedHectares > 0 ? s.PlannedHectares : l.Hectares,
                    s.PlannedDose,
                    s.PlannedTotal,
                    _breakdownPlannedTotal > 0 ? (s.PlannedTotal / _breakdownPlannedTotal) * 100 : 0
                ))).ToList();

            _realizedRows = laborsWithSupply.SelectMany(l => l.Supplies
                .Where(s => s.SupplyId == supplyId)
                .Select(s => new SupplyBreakdownRow(
                    l.LotName,
                    s.RealHectares ?? l.Hectares,
                    s.CalculatedDose ?? 0,
                    s.CalculatedTotal ?? 0,
                    _breakdownPlannedTotal > 0 ? (s.PlannedTotal / _breakdownPlannedTotal) * 100 : 0
                ))).ToList();

            _breakdownDrawerVisible = true;
            StateHasChanged();
        }

        protected string StatusTagStyle(string colorHex) => $"background: {colorHex}33; color: #fff; border: 1px solid {colorHex}80; border-radius: 12px; font-size: 11px; font-weight: 600; padding: 2px 10px; text-shadow: 0 1px 2px rgba(0,0,0,0.4);";
        protected string GetRowKey(LaborDto l) => l.Id.ToString();
        protected string StatusBadgeStyle(string colorHex) => $"display: inline-flex; align-items: center; gap: 6px; background: {colorHex}33; color: #fff; border: 1px solid {colorHex}80; border-radius: 12px; font-size: 12px; font-weight: 600; padding: 3px 10px; text-shadow: 0 1px 2px rgba(0,0,0,0.4);";
        protected string GetStatusColor(string status) => status switch {
            "Realized"           => "#2ECC71",
            "Validated"          => "#9B59B6",
            "AwaitingValidation" => "#F1C40F",
            _                    => "#3498DB"
        };
        protected string GetStatusLabel(string status) => status switch {
            "Realized"           => "Realizada",
            "Validated"          => "Validada",
            "AwaitingValidation" => "En Validación",
            _                    => "Planeada"
        };

        protected static string GetTypeBgColor(string laborType) => laborType switch
        {
            "Fertilizacion" => "#27AE60",
            "Pulverizacion" => "#2980B9",
            "Siembra" => "#8E44AD",
            "Cosecha" => "#D35400",
            "Monitoreo" => "#16A085",
            "Laboreo" => "#7F8C8D",
            _ => "#95A5A6"
        };
    }
}
