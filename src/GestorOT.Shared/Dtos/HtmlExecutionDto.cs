namespace GestorOT.Shared.Dtos;

public record HtmlExecutionRequest(
    string Token,
    Guid WorkOrderId,
    List<HtmlLaborResult> Labors
);

public record HtmlLaborResult(
    Guid Id,
    decimal RealHectares,
    List<HtmlSupplyResult> Supplies
);

public record HtmlSupplyResult(
    Guid Id,
    decimal RealDose
);
