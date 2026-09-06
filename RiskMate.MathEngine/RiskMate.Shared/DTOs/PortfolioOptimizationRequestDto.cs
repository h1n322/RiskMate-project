using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace RiskMate.Api.DTOs
{
    public class PortfolioOptimizationRequestDto
    {
        [Required]
        [MinLength(2)]
        public List<string> Tickers { get; set; } = new();

        [Range(0, 1)]
        public double RiskFreeRate { get; set; } = 0.04;

        [Range(100, 1_000_000)]
        public int SimulationsCount { get; set; } = 50000;

        [Required]
        public string Range { get; set; } = "5y";
    }
}