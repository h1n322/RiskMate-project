namespace RiskMate.Api.DTOs;

public class HistogramBinDto
{
    public string BinRange { get; set; } = string.Empty;
    public int Frequency { get; set; }
}