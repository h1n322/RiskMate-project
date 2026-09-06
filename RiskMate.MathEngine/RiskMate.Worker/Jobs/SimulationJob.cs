using System;
using System.Linq;
using System.Threading.Tasks;
using Hangfire.Server;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Caching.Distributed;
using System.Text.Json;
using RiskMate.Shared.Interfaces;
using RiskMate.Api.DTOs;
using RiskMate.Api.Services;
using RiskMate.MathEngine;
using RiskMate.MathEngine.Models;

namespace RiskMate.Worker.Jobs
{
    public class SimulationJob : ISimulationJob
    {
        private readonly YahooFinanceService _yahooFinanceService;
        private readonly RiskEngine _riskEngine;
        private readonly AiAnalyticsService _aiAnalyticsService;
        private readonly IDistributedCache _cache;
        private readonly ILogger<SimulationJob> _logger;

        public SimulationJob(
            YahooFinanceService yahooFinanceService,
            RiskEngine riskEngine,
            AiAnalyticsService aiAnalyticsService,
            IDistributedCache cache,
            ILogger<SimulationJob> logger)
        {
            _yahooFinanceService = yahooFinanceService;
            _riskEngine = riskEngine;
            _aiAnalyticsService = aiAnalyticsService;
            _cache = cache;
            _logger = logger;
        }

        public async Task ExecuteAsync(SimulationRequestDto dto, string userId, PerformContext context = null)
        {
            var jobId = context?.BackgroundJob.Id;
            _logger.LogInformation("Початок виконання симуляції. JobId: {JobId}, Ticker: {Ticker}", jobId, dto.Ticker);

            try
            {
                // Позначаємо статус як "В процесі"
                await SetJobStatusAsync(jobId, userId, new { Status = "Processing", Progress = 10 });

                bool isBacktest = dto.Algorithm?.ToLowerInvariant() == "backtest" || dto.IsBacktest;
                var algorithm = ParseAlgorithm(dto.Algorithm) ?? SimulationAlgorithm.Gbm;
                var scenario = ParseScenario(dto.Scenario);

                await SetJobStatusAsync(jobId, userId, new { Status = "Processing", Progress = 20, Message = "Fetching historical data" });
                var historyResponse = await _yahooFinanceService.GetHistoricalDataAsync(dto.Ticker, dto.LookbackYears);

                if (historyResponse?.data == null || historyResponse.data.Count < 10)
                {
                    throw new Exception($"Не вдалося отримати достатньо історичних даних для тикера {dto.Ticker}");
                }

                var priceDataPoints = historyResponse.data.Select(h => new PriceDataPoint
                {
                    Date = h.Date,
                    Price = h.Close
                }).ToList();

                await SetJobStatusAsync(jobId, userId, new { Status = "Processing", Progress = 50, Message = "Running mathematical simulation" });
                var simulationResult = _riskEngine.RunSimulation(
                    priceDataPoints,
                    algorithm,
                    dto.SimulationsCount,
                    dto.Horizon,
                    scenario,
                    dto.ConfidenceLevel,
                    dto.CustomShockPercentage ?? 0,
                    isBacktest,
                    dto.RiskFreeRate
                );

                await SetJobStatusAsync(jobId, userId, new { Status = "Processing", Progress = 80, Message = "Fetching AI analytics and news" });
                var news = await _yahooFinanceService.GetAssetNewsAsync(dto.Ticker);
                var aiSummary = await _aiAnalyticsService.GenerateRiskSummaryAsync(dto.Ticker, simulationResult, news);

                var finalResult = new {
                    Status = "Completed",
                    Progress = 100,
                    Result = new {
                        ExpectedPrice = simulationResult.ExpectedPrice,
                        ValueAtRisk = simulationResult.ValueAtRisk,
                        ConditionalValueAtRisk = simulationResult.ConditionalValueAtRisk,
                        Volatility = simulationResult.Volatility,
                        SharpeRatio = simulationResult.SharpeRatio,
                        MaxDrawdown = simulationResult.MaxDrawdown,
                        ChartPoints = simulationResult.ChartPoints.Select(p => new {
                            Name = p.Date.ToString("yyyy-MM-dd"),
                            History = p.History,
                            Forecast = p.Forecast,
                            Actual = p.Actual,
                            LowerBound = p.LowerBound,
                            UpperBound = p.UpperBound
                        }),
                        HistogramBins = simulationResult.HistogramBins.Select(b => new {
                            BinRange = b.MinValue == b.MaxValue 
                                ? $"${Math.Round(b.MinValue, 1)}"
                                : $"${Math.Round(b.MinValue, 1)}-${Math.Round(b.MaxValue, 1)}",
                            Frequency = b.Frequency
                        }),
                        Hedging = simulationResult.Hedging,
                        News = news,
                        AiSummary = aiSummary,
                        is_mock = historyResponse.is_mock
                    }
                };

                await SetJobStatusAsync(jobId, userId, finalResult);
                _logger.LogInformation("Симуляція успішно завершена. JobId: {JobId}", jobId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка виконання симуляції. JobId: {JobId}", jobId);
                await SetJobStatusAsync(jobId, userId, new { Status = "Error", Message = ex.Message });
                throw; // Щоб Hangfire міг зробити retry
            }
        }

        private async Task SetJobStatusAsync(string jobId, string userId, object statusObj)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            
            var options = new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24) // Зберігаємо результат добу
            };
            
            var json = JsonSerializer.Serialize(statusObj);
            await _cache.SetStringAsync($"sim_job_{userId}_{jobId}", json, options);
        }

        private static SimulationAlgorithm? ParseAlgorithm(string algorithm)
        {
            return algorithm?.ToLowerInvariant() switch
            {
                "gbm" or "montecarlo" or "monte_carlo" or "backtest" => SimulationAlgorithm.Gbm,
                "historical" => SimulationAlgorithm.Historical,
                "merton" => SimulationAlgorithm.Merton,
                "garch" => SimulationAlgorithm.Garch,
                _ => null
            };
        }

        private static StressScenario? ParseScenario(string scenario)
        {
            if (string.IsNullOrWhiteSpace(scenario)) return null;

            return scenario.ToLowerInvariant() switch
            {
                "base" or "none" or "default" => null,
                "covid" => StressScenario.Covid19Crash,
                "dotcom" => StressScenario.DotComBubble00,
                "crisis08" or "2008" => StressScenario.FinancialCrisis08,
                "blackmonday" => StressScenario.BlackMonday87,
                "war2022" => StressScenario.GeopoliticalShock22,
                "aibubble" => StressScenario.AIBubbleBurst,
                "flashcrash" => StressScenario.FlashCrash10,
                "custom" => StressScenario.CustomShock,
                _ => null
            };
        }
    }
}
