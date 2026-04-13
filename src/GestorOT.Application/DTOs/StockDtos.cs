namespace GestorOT.Application.DTOs;

public class StockValidationResult
{
    public bool IsValid { get; set; }
    public List<StockShortage> Shortages { get; set; } = new();
}

public class StockShortage
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public double Available { get; set; }
    public decimal Required { get; set; }
    public double Deficit { get; set; }
}
