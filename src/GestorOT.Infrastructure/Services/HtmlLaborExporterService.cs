using System.Text;
using GestorOT.Application.Interfaces;
using GestorOT.Application.Services;
using GestorOT.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GestorOT.Infrastructure.Services;

public class HtmlLaborExporterService : IHtmlLaborExporterService
{
    private readonly IApplicationDbContext _context;
    private readonly IWorkOrderQueryService _queryService;
    private readonly IConfiguration _configuration;

    public HtmlLaborExporterService(
        IApplicationDbContext context,
        IWorkOrderQueryService queryService,
        IConfiguration configuration)
    {
        _context = context;
        _queryService = queryService;
        _configuration = configuration;
    }

    public async Task<string> GenerateInteractiveHtmlAsync(Guid workOrderId, string baseUrl, CancellationToken ct = default)
    {
        var wo = await _queryService.GetByIdAsync(workOrderId, ct);
        if (wo == null) return string.Empty;

        // Generate Token
        var token = Guid.NewGuid().ToString("N");
        var sharedToken = new SharedToken
        {
            Id = Guid.NewGuid(),
            WorkOrderId = workOrderId,
            TokenHash = token, // Using plain for this simple flow as per sprint doc "token de un solo uso"
            ExpiresAt = DateTime.UtcNow.AddDays(30),
            CreatedAt = DateTime.UtcNow,
            IsUsed = false
        };

        _context.SharedTokens.Add(sharedToken);
        await _context.SaveChangesAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("<!DOCTYPE html>");
        sb.AppendLine("<html lang='es'>");
        sb.AppendLine("<head>");
        sb.AppendLine("    <meta charset='UTF-8'>");
        sb.AppendLine("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
        sb.AppendLine($"    <title>Reporte de Labor - {wo.OTNumber}</title>");
        sb.AppendLine("    <style>");
        sb.AppendLine("        body { font-family: -apple-system, system-ui, sans-serif; background: #f4f7f9; color: #333; margin: 0; padding: 20px; }");
        sb.AppendLine("        .container { max-width: 600px; margin: 0 auto; background: white; padding: 25px; border-radius: 12px; box-shadow: 0 4px 20px rgba(0,0,0,0.08); }");
        sb.AppendLine("        h1 { font-size: 20px; margin-top: 0; color: #1a202c; }");
        sb.AppendLine("        .header-info { font-size: 14px; color: #718096; margin-bottom: 20px; border-bottom: 1px solid #edf2f7; padding-bottom: 15px; }");
        sb.AppendLine("        .labor-card { background: #f8fafc; border: 1px solid #e2e8f0; border-radius: 8px; padding: 15px; margin-bottom: 15px; }");
        sb.AppendLine("        .labor-title { font-weight: bold; font-size: 15px; margin-bottom: 10px; display: block; }");
        sb.AppendLine("        .supply-row { display: grid; grid-template-columns: 1fr 100px; gap: 10px; margin-bottom: 10px; align-items: end; }");
        sb.AppendLine("        label { font-size: 12px; color: #4a5568; display: block; margin-bottom: 4px; }");
        sb.AppendLine("        input { width: 100%; padding: 8px; border: 1px solid #cbd5e0; border-radius: 4px; box-sizing: border-box; }");
        sb.AppendLine("        button { width: 100%; padding: 12px; background: #3182ce; color: white; border: none; border-radius: 6px; font-weight: bold; cursor: pointer; margin-top: 20px; }");
        sb.AppendLine("        button:disabled { background: #a0aec0; }");
        sb.AppendLine("        .status-msg { margin-top: 15px; padding: 12px; border-radius: 6px; display: none; font-size: 14px; }");
        sb.AppendLine("        .success { background: #c6f6d5; color: #22543d; border: 1px solid #9ae6b4; }");
        sb.AppendLine("        .error { background: #fed7d7; color: #822727; border: 1px solid #feb2b2; }");
        sb.AppendLine("    </style>");
        sb.AppendLine("</head>");
        sb.AppendLine("<body>");
        sb.AppendLine("    <div class='container'>");
        sb.AppendLine($"        <h1>Orden de Trabajo: {wo.OTNumber}</h1>");
        sb.AppendLine("        <div class='header-info'>");
        sb.AppendLine($"            <div><strong>Campo:</strong> {wo.FieldName}</div>");
        sb.AppendLine($"            <div><strong>Descripción:</strong> {wo.Description}</div>");
        sb.AppendLine("        </div>");
        sb.AppendLine("        <form id='laborForm'>");

        foreach (var labor in wo.Labors.Where(l => l.Mode == "Planned"))
        {
            sb.AppendLine("            <div class='labor-card'>");
            sb.AppendLine($"                <span class='labor-title'>{labor.LaborTypeName} - {labor.LotName}</span>");
            sb.AppendLine("                <div style='margin-bottom: 10px;'>");
            sb.AppendLine($"                    <label>Hectáreas Reales (Planificadas: {labor.Hectares} ha)</label>");
            sb.AppendLine($"                    <input type='number' step='0.01' name='labor_ha_{labor.Id}' value='{labor.Hectares}' required>");
            sb.AppendLine("                </div>");
            
            foreach (var supply in labor.Supplies)
            {
                sb.AppendLine("                <div class='supply-row'>");
                sb.AppendLine("                    <div>");
                sb.AppendLine($"                        <label>{supply.SupplyName} (Plan: {supply.PlannedDose} {supply.UnitOfMeasure}/ha)</label>");
                sb.AppendLine($"                        <input type='number' step='0.01' name='supply_dose_{supply.Id}' value='{supply.PlannedDose}' required>");
                sb.AppendLine("                    </div>");
                sb.AppendLine("                    <div style='font-size: 11px; color: #718096; padding-bottom: 10px;'>");
                sb.AppendLine($"                        {supply.UnitOfMeasure}/ha");
                sb.AppendLine("                    </div>");
                sb.AppendLine("                </div>");
            }
            sb.AppendLine("            </div>");
        }

        sb.AppendLine("            <button type='submit' id='submitBtn'>Enviar Datos de Campo</button>");
        sb.AppendLine("        </form>");
        sb.AppendLine("        <div id='statusMsg' class='status-msg'></div>");
        sb.AppendLine("    </div>");

        sb.AppendLine("    <script>");
        sb.AppendLine("        const form = document.getElementById('laborForm');");
        sb.AppendLine("        const btn = document.getElementById('submitBtn');");
        sb.AppendLine("        const status = document.getElementById('statusMsg');");
        sb.AppendLine("");
        sb.AppendLine("        form.addEventListener('submit', async (e) => {");
        sb.AppendLine("            e.preventDefault();");
        sb.AppendLine("            btn.disabled = true;");
        sb.AppendLine("            status.style.display = 'none';");
        sb.AppendLine("");
        sb.AppendLine("            const formData = new FormData(form);");
        sb.AppendLine("            const data = {");
        sb.AppendLine($"                token: '{token}',");
        sb.AppendLine($"                workOrderId: '{workOrderId}',");
        sb.AppendLine("                labors: []");
        sb.AppendLine("            };");
        sb.AppendLine("");
        // Build the structure from formData
        sb.AppendLine("            const laborsMap = {};");
        sb.AppendLine("            for (let [key, value] of formData.entries()) {");
        sb.AppendLine("                if (key.startsWith('labor_ha_')) {");
        sb.AppendLine("                    const id = key.replace('labor_ha_', '');");
        sb.AppendLine("                    laborsMap[id] = { id, realHectares: parseFloat(value), supplies: [] };");
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            for (let [key, value] of formData.entries()) {");
        sb.AppendLine("                if (key.startsWith('supply_dose_')) {");
        sb.AppendLine("                    const id = key.replace('supply_dose_', '');");
        // We need to know which labor this supply belongs to. 
        // For simplicity in this generated JS, we'll iterate the labors in the DTO to find the mapping.
        foreach (var labor in wo.Labors) {
            foreach (var supply in labor.Supplies) {
                sb.AppendLine($"                    if (id === '{supply.Id}') laborsMap['{labor.Id}'].supplies.push({{ id, realDose: parseFloat(value) }});");
            }
        }
        sb.AppendLine("                }");
        sb.AppendLine("            }");
        sb.AppendLine("            data.labors = Object.values(laborsMap);");
        sb.AppendLine("");
        sb.AppendLine("            try {");
        sb.AppendLine($"                const response = await fetch('{baseUrl}/api/share/realize-from-html', {{");
        sb.AppendLine("                    method: 'POST',");
        sb.AppendLine("                    headers: { 'Content-Type': 'application/json' },");
        sb.AppendLine("                    body: JSON.stringify(data)");
        sb.AppendLine("                }});");
        sb.AppendLine("");
        sb.AppendLine("                if (response.ok) {");
        sb.AppendLine("                    status.textContent = 'Datos enviados con éxito. La labor ha sido registrada.';");
        sb.AppendLine("                    status.className = 'status-msg success';");
        sb.AppendLine("                    form.style.display = 'none';");
        sb.AppendLine("                } else {");
        sb.AppendLine("                    const err = await response.text();");
        sb.AppendLine("                    status.textContent = 'Error: ' + err;");
        sb.AppendLine("                    status.className = 'status-msg error';");
        sb.AppendLine("                    btn.disabled = false;");
        sb.AppendLine("                }");
        sb.AppendLine("                status.style.display = 'block';");
        sb.AppendLine("            } catch (e) {");
        sb.AppendLine("                status.textContent = 'Error de conexión: ' + e.message;");
        sb.AppendLine("                status.className = 'status-msg error';");
        sb.AppendLine("                status.style.display = 'block';");
        sb.AppendLine("                btn.disabled = false;");
        sb.AppendLine("            }");
        sb.AppendLine("        });");
        sb.AppendLine("    </script>");
        sb.AppendLine("</body>");
        sb.AppendLine("</html>");

        return sb.ToString();
    }
}
