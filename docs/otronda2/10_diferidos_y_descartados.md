## LOT-04
Se decidió mantener la navegación actual a las pantallas de GIS y Rotaciones, ya que el contexto de estas pantallas es suficientemente distinto como para requerir su propia vista completa.

## BUG-02
Análisis: `Campanias.razor` utiliza `api/campaigns` (todas las campañas), mientras que `CampaignSelector` utiliza `api/campaigns/active` (filtrado por `IsActive` y `Status != "Locked"`). La discrepancia donde una campaña aparece en el selector pero no en la lista parece deberse a una posible inconsistencia en el filtrado por `Tenant` en `_context.Campaigns` o un issue en la lógica de `GetCampaigns` en `CampaignsController.cs` que podría estar aplicando filtros inesperados. Como es un issue que requiere investigación de datos en la DB, se deja como nota de auditoría.
