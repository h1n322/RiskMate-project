namespace RiskMate.Api.DTOs;

public class ChartPointDto
{
    public string DateLabel { get; set; } = string.Empty;
    public decimal ExpectedPrice { get; set; }
    public decimal LowerBound { get; set; }
    public decimal UpperBound { get; set; }
}
