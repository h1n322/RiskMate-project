using System.Collections.Generic;

namespace RiskMate.Api.DTOs
{
    public class PortfolioCreateDto
    {
        public string Tickers { get; set; } = string.Empty;
        public string Algorithm { get; set; } = string.Empty;
        public int SimulationsCount { get; set; }
        public int Horizon { get; set; }
        public string Scenario { get; set; } = string.Empty;

        public decimal ExpectedPrice { get; set; }
        public decimal ValueAtRisk { get; set; }
        public decimal ConditionalValueAtRisk { get; set; }
        public decimal Volatility { get; set; }
        public decimal SharpeRatio { get; set; }
        public decimal MaxDrawdown { get; set; }

        public List<ChartPointDto> ChartPoints { get; set; } = [];
        public List<AssetDetailDto> AssetDetails { get; set; } = [];
        public List<HistogramBinDto> HistogramBins { get; set; } = [];
    }





}