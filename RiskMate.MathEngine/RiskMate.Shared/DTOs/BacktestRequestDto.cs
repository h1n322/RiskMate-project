using System.ComponentModel.DataAnnotations;

namespace RiskMate.Api.DTOs
{
    public class BacktestRequestDto
    {
        [Required]
        public string Ticker { get; set; } = "AAPL";

        [Range(1, 10_000)]
        public int WindowSize { get; set; } = 252;

        [Range(0.01, 0.99)]
        public double ConfidenceLevel { get; set; } = 0.95;

        [Required]
        public string Range { get; set; } = "5y";
    }
}