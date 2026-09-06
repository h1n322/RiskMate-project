using System.ComponentModel.DataAnnotations;

namespace RiskMate.Api.DTOs
{
    public class SimulationRequestDto
    {
        [Required]
        public string Ticker { get; set; } = "AAPL";

        [Required]
        public string Algorithm { get; set; } = "gbm";

        [Range(1, 1_000_000)]
        public int SimulationsCount { get; set; } = 1000;

        [Range(1, 2520)]
        public int Horizon { get; set; } = 30;

        public string Scenario { get; set; } = "Base";

        [Range(0.01, 0.99)]
        public double ConfidenceLevel { get; set; } = 0.95; // Наші 90%, 95% або 99%

        [Range(0, 0.99)]
        public double? CustomShockPercentage { get; set; } // Для сценарію "custom" (частка падіння, напр. 0.2 = -20%)

        public bool IsBacktest { get; set; } = false;

        [Range(1, 20)]
        public int LookbackYears { get; set; } = 5;

        [Range(0.0, 0.5)]
        public double RiskFreeRate { get; set; } = 0.045;
    }
}