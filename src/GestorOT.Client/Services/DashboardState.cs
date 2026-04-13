using GestorOT.Shared.Dtos;

namespace GestorOT.Client.Services;

public class DashboardState
{
    public DashboardStatsDto? Stats { get; set; }
    public List<RecentWorkOrderDto>? RecentOrders { get; set; }
    public GeoJsonFeatureCollection? CachedGeoJson { get; set; }
    public DateTime? LastSync { get; set; }
    public bool IsLoading { get; set; }
    public bool Initialized => Stats != null;

    public event Action? OnChange;

    public void SetData(DashboardStatsDto stats, List<RecentWorkOrderDto> orders, GeoJsonFeatureCollection? geoJson)
    {
        Stats = stats;
        RecentOrders = orders;
        CachedGeoJson = geoJson;
        LastSync = DateTime.Now;
        NotifyStateChanged();
    }

    public void SetLoading(bool loading)
    {
        IsLoading = loading;
        NotifyStateChanged();
    }

    public void Clear()
    {
        Stats = null;
        RecentOrders = null;
        CachedGeoJson = null;
        LastSync = null;
        NotifyStateChanged();
    }

    private void NotifyStateChanged() => OnChange?.Invoke();
}
