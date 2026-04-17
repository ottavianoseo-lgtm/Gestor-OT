using System.ComponentModel.DataAnnotations;

namespace GestorOT.Shared.Validation;

public class CreateFieldRequest
{
    [Required(ErrorMessage = "El nombre del campo es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string Name { get; set; } = string.Empty;
}

public class CreateLotRequest
{
    [Required(ErrorMessage = "El FieldId es obligatorio.")]
    public Guid FieldId { get; set; }

    [Required(ErrorMessage = "El nombre del lote es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "El estado no puede exceder 50 caracteres.")]
    public string Status { get; set; } = "Active";

    public string? WktGeometry { get; set; }
}

public class CreateWorkOrderRequest
{
    [Required(ErrorMessage = "El LotId es obligatorio.")]
    public Guid LotId { get; set; }

    [Required(ErrorMessage = "La descripción es obligatoria.")]
    [StringLength(1000, MinimumLength = 3, ErrorMessage = "La descripción debe tener entre 3 y 1000 caracteres.")]
    public string Description { get; set; } = string.Empty;

    [StringLength(50, ErrorMessage = "El estado no puede exceder 50 caracteres.")]
    public string Status { get; set; } = "Draft";

    [Required(ErrorMessage = "El campo 'Asignado a' es obligatorio.")]
    [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres.")]
    public string AssignedTo { get; set; } = string.Empty;

    public DateTime DueDate { get; set; }

    [StringLength(50)]
    public string? OTNumber { get; set; }

    public DateTime? PlannedDate { get; set; }
    public DateTime? ExpirationDate { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El costo estimado debe ser mayor o igual a 0.")]
    public decimal EstimatedCostUSD { get; set; }

    public bool StockReserved { get; set; }
    public Guid? ContractorId { get; set; }
    public Guid? CampaignId { get; set; }
}

public class CreateCampaignRequest
{
    [Required(ErrorMessage = "El nombre de la campaña es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; } = true;

    [Required(ErrorMessage = "El estado es obligatorio.")]
    [StringLength(50)]
    public string Status { get; set; } = "Active";

    [Range(0, double.MaxValue, ErrorMessage = "El presupuesto debe ser mayor o igual a 0.")]
    public decimal BudgetTotalUSD { get; set; }

    public string? BusinessRulesJson { get; set; }
    public List<CampaignFieldRequest>? Fields { get; set; }
}

public class CampaignFieldRequest
{
    [Required(ErrorMessage = "El FieldId es obligatorio.")]
    public Guid FieldId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "El rendimiento objetivo debe ser mayor o igual a 0.")]
    public decimal TargetYieldTonHa { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Las hectáreas asignadas deben ser mayor o igual a 0.")]
    public decimal AllocatedHectares { get; set; }
}

public class CreateStrategyRequest
{
    [Required(ErrorMessage = "El nombre de la estrategia es obligatorio.")]
    [StringLength(200, MinimumLength = 2, ErrorMessage = "El nombre debe tener entre 2 y 200 caracteres.")]
    public string Name { get; set; } = string.Empty;

    [StringLength(100, ErrorMessage = "El tipo de cultivo no puede exceder 100 caracteres.")]
    public string CropType { get; set; } = string.Empty;

    public List<StrategyItemRequest>? Items { get; set; }
}

public class StrategyItemRequest
{
    [Required(ErrorMessage = "El tipo de labor es obligatorio.")]
    [StringLength(100, MinimumLength = 2, ErrorMessage = "El tipo de labor debe tener entre 2 y 100 caracteres.")]
    public string LaborType { get; set; } = string.Empty;

    public int DayOffset { get; set; }

    public List<StrategySupplyRequest>? DefaultSupplies { get; set; }
}

public class StrategySupplyRequest
{
    [Required(ErrorMessage = "El SupplyId es obligatorio.")]
    public Guid SupplyId { get; set; }

    [Range(0.0001, double.MaxValue, ErrorMessage = "La dosis debe ser mayor a 0.")]
    public decimal Dose { get; set; }

    [Required(ErrorMessage = "La unidad de dosis es obligatoria.")]
    [StringLength(50)]
    public string DoseUnit { get; set; } = string.Empty;
}

public class RealizeLaborRequest
{
    [Required(ErrorMessage = "La lista de insumos es obligatoria.")]
    [MinLength(1, ErrorMessage = "Debe proporcionar al menos un insumo.")]
    public List<LaborSupplyRealRequest> Supplies { get; set; } = [];
}

public class LaborSupplyRealRequest
{
    [Required(ErrorMessage = "El Id del insumo es obligatorio.")]
    public Guid Id { get; set; }

    public decimal PlannedDose { get; set; }
    public decimal? RealDose { get; set; }
}
