using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class ContextState
{
    public event Action? OnChange;

    public LoteResumenDto? LoteSeleccionado { get; private set; }
    public LaborDetalleDto? LaborSeleccionada { get; private set; }
    public bool IsPanelOpen { get; private set; }

    public void SeleccionarLote(LoteResumenDto lote)
    {
        LoteSeleccionado = lote;
        LaborSeleccionada = null;
        IsPanelOpen = true;
        Notify();
    }

    public void SeleccionarLabor(LaborDetalleDto labor)
    {
        LaborSeleccionada = labor;
        LoteSeleccionado = null;
        IsPanelOpen = true;
        Notify();
    }

    public void CerrarPanel()
    {
        IsPanelOpen = false;
        LoteSeleccionado = null;
        LaborSeleccionada = null;
        Notify();
    }

    private void Notify() => OnChange?.Invoke();
}
