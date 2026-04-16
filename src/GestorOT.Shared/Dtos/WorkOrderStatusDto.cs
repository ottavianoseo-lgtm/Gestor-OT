namespace GestorOT.Shared.Dtos;

public class WorkOrderStatusDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ColorHex { get; set; } = "#3498DB";
    public bool IsEditable { get; set; } = true;
    public bool IsDefault { get; set; } = false;
    public int SortOrder { get; set; }

    public WorkOrderStatusDto() { }
    public WorkOrderStatusDto(Guid id, string name, string colorHex, bool isEditable, bool isDefault, int sortOrder)
    {
        Id = id; Name = name; ColorHex = colorHex; IsEditable = isEditable; IsDefault = isDefault; SortOrder = sortOrder;
    }
}
