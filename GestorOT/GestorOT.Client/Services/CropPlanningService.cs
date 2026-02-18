using System.Net.Http.Json;
using GestorOT.Shared;
using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class CropPlanningService
{
    private readonly HttpClient _http;

    public CropPlanningService(HttpClient http)
    {
        _http = http;
    }

    public async Task<List<CultivoDto>> GetCultivos(CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync("api/cultivos",
            AppJsonSerializerContext.Default.ListCultivoDto, ct) ?? new();
    }

    public async Task<List<PlanificacionCultivoDto>> GetPlanificacion(Guid campanaId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync($"api/planificacion-cultivos?campanaId={campanaId}",
            AppJsonSerializerContext.Default.ListPlanificacionCultivoDto, ct) ?? new();
    }

    public async Task<PlanificacionCultivoDto?> CreatePlanificacion(PlanificacionCultivoDto dto, CancellationToken ct = default)
    {
        var response = await _http.PostAsJsonAsync("api/planificacion-cultivos",
            dto, AppJsonSerializerContext.Default.PlanificacionCultivoDto, ct);
        if (response.IsSuccessStatusCode)
            return await response.Content.ReadFromJsonAsync(AppJsonSerializerContext.Default.PlanificacionCultivoDto, ct);
        return null;
    }

    public async Task<bool> UpdatePlanificacion(Guid id, PlanificacionCultivoDto dto, CancellationToken ct = default)
    {
        var response = await _http.PutAsJsonAsync($"api/planificacion-cultivos/{id}",
            dto, AppJsonSerializerContext.Default.PlanificacionCultivoDto, ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> DeletePlanificacion(Guid id, CancellationToken ct = default)
    {
        var response = await _http.DeleteAsync($"api/planificacion-cultivos/{id}", ct);
        return response.IsSuccessStatusCode;
    }

    public async Task<SuperficieCampoDto?> GetSuperficieCampo(Guid campoId, Guid campanaId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync($"api/campos/{campoId}/campanas/{campanaId}/superficie",
            AppJsonSerializerContext.Default.SuperficieCampoDto, ct);
    }

    public async Task<RotacionHistorialDto?> GetRotacion(Guid loteId, CancellationToken ct = default)
    {
        return await _http.GetFromJsonAsync($"api/lotes/{loteId}/rotacion",
            AppJsonSerializerContext.Default.RotacionHistorialDto, ct);
    }
}
