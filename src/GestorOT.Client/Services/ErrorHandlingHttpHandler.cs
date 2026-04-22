using System.Net;
using System.Net.Http.Json;
using GestorOT.Shared.Dtos;
using Microsoft.AspNetCore.Components;

namespace GestorOT.Client.Services;

public class ErrorHandlingHttpHandler : DelegatingHandler
{
    private readonly NavigationManager _nav;
    private readonly LoadingService _loading;

    public ErrorHandlingHttpHandler(NavigationManager nav, LoadingService loading)
    {
        _nav = nav;
        _loading = loading;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _loading.Show();

        try
        {
            var response = await base.SendAsync(request, cancellationToken);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                _loading.ShowError("Sesión expirada. Recargue la página.");
            }
            else if (response.StatusCode == HttpStatusCode.Forbidden)
            {
                _loading.ShowError("No tiene permisos para esta acción.");
            }
            else if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                var errorMsg = await response.Content.ReadAsStringAsync();
                if (string.IsNullOrEmpty(errorMsg)) errorMsg = "Solicitud inválida.";
                
                // If it's JSON, try to extract 'error' or 'message' field
                try {
                    using var doc = System.Text.Json.JsonDocument.Parse(errorMsg);
                    if (doc.RootElement.TryGetProperty("error", out var errProp)) errorMsg = errProp.GetString() ?? errorMsg;
                    else if (doc.RootElement.TryGetProperty("message", out var msgProp)) errorMsg = msgProp.GetString() ?? errorMsg;
                } catch { /* Not JSON or different format, keep original string */ }

                _loading.ShowError(errorMsg);
            }
            else if (response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _loading.ShowError("Demasiadas solicitudes. Espere un momento.");
            }
            else if (response.StatusCode >= HttpStatusCode.InternalServerError)
            {
                _loading.ShowError("Error del servidor. Intente nuevamente.");
            }

            return response;
        }
        catch (HttpRequestException)
        {
            _loading.ShowError("No se pudo conectar con el servidor.");
            throw;
        }
        catch (TaskCanceledException)
        {
            _loading.ShowError("La solicitud fue cancelada por timeout.");
            throw;
        }
        finally
        {
            _loading.Hide();
        }
    }
}
