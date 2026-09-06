using RiskMate.MathEngine.Simulators.Interfaces;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Calculators;
using RiskMate.MathEngine.Simulators;
using RiskMate.MathEngine.Generators;

namespace RiskMate.MathEngine
{
    public class RiskEngine
    {
        private readonly IMonteCarloSimulator _monteCarlo;
        private readonly IHistoricalSimulator _historical;
        private readonly IStressTestSimulator _stressTest;
        private readonly IMertonJumpSimulator _merton;
        private readonly IGarchSimulator _garch;
        private readonly ILogger<RiskEngine> _logger;

        public RiskEngine(
            IMonteCarloSimulator monteCarlo,
            IHistoricalSimulator historical,
            IStressTestSimulator stressTest,
            IMertonJumpSimulator merton,
            IGarchSimulator garch,
            ILogger<RiskEngine> logger)
        {
            _monteCarlo = monteCarlo;
            _historical = historical;
            _stressTest = stressTest;
            _merton = merton;
            _garch = garch;
            _logger = logger;
        }

        public SimulationResult RunSimulation(
            List<double> historicalPrices,
            SimulationAlgorithm algorithm,
            int simulationsCount,
            int horizon,
            StressScenario? scenario = null,
            double confidenceLevel = 0.95,
            double customShockPercentage = 0,
            double riskFreeRate = 0.045)
        {
            var startDate = DateTime.UtcNow.AddDays(-historicalPrices.Count);
            var priceData = historicalPrices.Select((p, i) => new PriceDataPoint { Date = startDate.AddDays(i), Price = p }).ToList();
            return RunSimulation(priceData, algorithm, simulationsCount, horizon, scenario, confidenceLevel, customShockPercentage, false, riskFreeRate);
        }

        public SimulationResult RunSimulation(
            List<PriceDataPoint> historicalData,
            SimulationAlgorithm algorithm,
            int simulationsCount,
            int horizon,
            StressScenario? scenario = null,
            double confidenceLevel = 0.95,
            double customShockPercentage = 0,
            bool isBacktest = false,
            double riskFreeRate = 0.045)
        {
            if (historicalData == null || historicalData.Count == 0)
            {
                throw new ArgumentException("Історичні дані не можуть бути порожніми.", nameof(historicalData));
            }

            List<PriceDataPoint> validationData = null;
            if (isBacktest && historicalData.Count > horizon)
            {
                validationData = historicalData.Skip(historicalData.Count - horizon).ToList();
                historicalData = historicalData.Take(historicalData.Count - horizon).ToList();
            }

            var historicalPrices = historicalData.Select(h => h.Price).ToList();
            var returns = ReturnsCalculator.CalculateLogReturns(historicalPrices);
            if (returns.Count == 0)
            {
                throw new ArgumentException("Недостатньо коректних історичних даних для розрахунку дохідностей.", nameof(historicalData));
            }

            double currentPrice = historicalPrices.Last();
            
            // Встановлюємо детерміністичний сід, щоб генерація PDF збігалася з відображенням на Dashboard
            int seed = (currentPrice + horizon + simulationsCount + confidenceLevel * 100).GetHashCode();
            IRandomProvider rng = new RandomProvider(seed);
            
            double meanReturn = returns.Average();
            double volatility = RiskCalculator.CalculateVolatility(returns);
            double drift = DriftCalculator.CalculateGbmDrift(meanReturn, volatility);

            var parameters = new AssetParameters
            {
                InitialPrice = currentPrice,
                MeanReturn = meanReturn,
                Volatility = volatility,
                Drift = drift
            };

            double[][] paths;

            switch (algorithm)
            {
                case SimulationAlgorithm.Historical:
                    paths = _historical.Simulate(currentPrice, returns, simulationsCount, horizon, rng);
                    break;
                case SimulationAlgorithm.Merton:
                    paths = _merton.Simulate(parameters, simulationsCount, horizon, 2.0, 0.0, 0.1, rng);
                    break;
                case SimulationAlgorithm.Garch:
                    paths = _garch.Simulate(parameters, simulationsCount, horizon, 0.00001, 0.1, 0.85, rng);
                    break;
                default:
                    if (scenario.HasValue)
                    {
                        paths = _stressTest.Simulate(parameters, simulationsCount, horizon, scenario.Value, customShockPercentage, rng);
                    }
                    else
                    {
                        paths = _monteCarlo.Simulate(parameters, simulationsCount, horizon, rng);
                    }
                    break;
            }

            var metrics = MetricsCalculator.CalculateMetrics(paths, confidenceLevel);

            double expectedReturnAnn = ((metrics.ExpectedPrice - currentPrice) / currentPrice) * (Constants.TradingDaysPerYear / (double)horizon);
            double sharpeRatio = (expectedReturnAnn - riskFreeRate) / (volatility * Math.Sqrt(Constants.TradingDaysPerYear));

            double maxPeak = historicalPrices[0];
            double maxDrawdown = 0;
            foreach (var price in historicalPrices)
            {
                if (price > maxPeak) maxPeak = price;
                double drawdown = (maxPeak - price) / maxPeak;
                if (drawdown > maxDrawdown) maxDrawdown = drawdown;
            }

            var result = new SimulationResult
            {
                ExpectedPrice = metrics.ExpectedPrice,
                ValueAtRisk = metrics.ValueAtRisk,
                ConditionalValueAtRisk = metrics.ConditionalValueAtRisk,
                Volatility = volatility * Math.Sqrt(Constants.TradingDaysPerYear) * 100.0,
                SharpeRatio = sharpeRatio,
                MaxDrawdown = maxDrawdown * 100.0
            };

            double strikePrice = currentPrice - Math.Abs(metrics.ValueAtRisk);
            if (strikePrice > 0 && horizon > 0 && algorithm != SimulationAlgorithm.Markowitz)
            {
                double timeToExpirationYears = horizon / (double)Constants.TradingDaysPerYear;
                
                result.Hedging = Options.BlackScholesCalculator.CalculatePutOption(
                    currentPrice: currentPrice,
                    strikePrice: strikePrice,
                    timeToExpirationYears: timeToExpirationYears,
                    riskFreeRate: riskFreeRate, 
                    volatility: volatility * Math.Sqrt(Constants.TradingDaysPerYear)
                );
            }

            // 1. Додаємо історичні дані
            for (int i = 0; i < historicalData.Count; i++)
            {
                var h = historicalData[i];
                // Для останньої історичної точки встановимо також Forecast = Price, щоб лінії з'єдналися
                double? forecastVal = (i == historicalData.Count - 1) ? h.Price : null;

                result.ChartPoints.Add(new ChartPointData
                {
                    Date = h.Date,
                    History = h.Price,
                    Forecast = forecastVal,
                    LowerBound = null,
                    UpperBound = null
                });
            }

            // 2. Додаємо симуляційний прогноз (майбутні дні)
            // Вибираємо репрезентативну (медіанну) траєкторію з реалістичною стохастичною волатильністю
            var finalPricesIndexed = paths
                .Select((p, idx) => new { Index = idx, FinalPrice = p.Last() })
                .OrderBy(x => x.FinalPrice)
                .ToList();
            int medianPathIndex = finalPricesIndexed[finalPricesIndexed.Count / 2].Index;
            var representativePath = paths[medianPathIndex];

            double lowerPercentile = (1.0 - confidenceLevel) / 2.0;
            double upperPercentile = 1.0 - lowerPercentile;
            var lastDate = historicalData.Last().Date;

            for (int day = 1; day <= horizon; day++)
            {
                var futureDate = lastDate.AddDays(day);
                var dayPrices = paths.Select(p => p[day]).ToList();
                dayPrices.Sort();

                int lowerIndex = (int)Math.Floor(dayPrices.Count * lowerPercentile);
                int upperIndex = (int)Math.Ceiling(dayPrices.Count * upperPercentile) - 1;
                lowerIndex = Math.Clamp(lowerIndex, 0, dayPrices.Count - 1);
                upperIndex = Math.Clamp(upperIndex, 0, dayPrices.Count - 1);

                double? actualVal = null;
                if (isBacktest && validationData != null && day <= validationData.Count)
                {
                    actualVal = validationData[day - 1].Price;
                }

                result.ChartPoints.Add(new ChartPointData
                {
                    Date = futureDate,
                    History = null,
                    Forecast = Math.Round(representativePath[day], 2),
                    Actual = actualVal,
                    LowerBound = Math.Round(dayPrices[lowerIndex], 2),
                    UpperBound = Math.Round(dayPrices[upperIndex], 2)
                });
            }

            result.HistogramBins = GenerateHistogram(paths.Select(p => p[p.Length - 1]).ToArray());

            return result;
        }

        private List<HistogramBinData> GenerateHistogram(double[] finalPrices, int binCount = 15)
        {
            var bins = new List<HistogramBinData>();
            double min = finalPrices.Min();
            double max = finalPrices.Max();
            double range = max - min;

            if (range <= 0)
            {
                bins.Add(new HistogramBinData
                {
                    MinValue = min, MaxValue = max,
                    Frequency = finalPrices.Length
                });
                return bins;
            }

            double binWidth = range / binCount;

            var counts = new int[binCount];
            foreach (var price in finalPrices)
            {
                int binIndex = (int)((price - min) / binWidth);
                if (binIndex >= binCount) binIndex = binCount - 1;
                if (binIndex < 0) binIndex = 0;
                counts[binIndex]++;
            }

            for (int i = 0; i < binCount; i++)
            {
                double binMin = min + (i * binWidth);
                double binMax = binMin + binWidth;
                bins.Add(new HistogramBinData
                {
                    MinValue = binMin, MaxValue = binMax,
                    Frequency = counts[i]
                });
            }

            return bins;
        }
    }
}