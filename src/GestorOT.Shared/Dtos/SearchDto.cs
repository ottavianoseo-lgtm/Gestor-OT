namespace GestorOT.Shared.Dtos;

public record SearchResult(
    List<WorkOrderDto> WorkOrders,
    List<LaborDto> Labors,
    List<LotDto> Lots,
    int Total
)
{
    public SearchResult() : this(new(), new(), new(), 0) { }
}
