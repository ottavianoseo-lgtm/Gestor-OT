namespace GestorOT.Shared.Dtos;

public record ErpGroupSummaryDto(
    string GroupName,
    int ItemCount,
    List<string> SampleItems,
    bool IsSelected
);

public record SyncInventoryGroupsRequestDto(
    List<string> SelectedGroups,
    bool CleanUnselected = true
);

public record SyncInventoryGroupsResultDto(
    bool Success,
    int SyncedCount,
    int CleanedCount,
    string Message
);
