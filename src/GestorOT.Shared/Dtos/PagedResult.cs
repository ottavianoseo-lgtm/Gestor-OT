namespace GestorOT.Shared.Dtos;

public record PagedResult<T>(List<T> Items, int Total, int Page, int PageSize)
{
    public int TotalPages => (int)Math.Ceiling((double)Total / PageSize);
    public bool HasPrevious => Page > 1;
    public bool HasNext => Page < TotalPages;

    public PagedResult() : this(new List<T>(), 0, 1, 50) { }
}
