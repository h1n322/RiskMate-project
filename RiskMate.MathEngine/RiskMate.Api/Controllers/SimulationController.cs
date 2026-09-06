using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Hangfire;
using Microsoft.Extensions.Caching.Distributed;
using RiskMate.Api.DTOs;
using RiskMate.Api.Services;
using RiskMate.MathEngine;
using RiskMate.MathEngine.Models;
using RiskMate.MathEngine.Simulators;
using RiskMate.Shared.Interfaces;
using System.Threading.Tasks;
using System.Linq;

namespace RiskMate.Api.Controllers
{
    //[Authorize] 
    [ApiController]
    [Route("api/[controller]")]
    public class SimulationController : ControllerBase
    {
        private readonly IBackgroundJobClient _backgroundJobs;
        private readonly IDistributedCache _cache;
        private readonly ILogger<SimulationController> _logger;

        // Зберігаємо старі сервіси для бектесту/PDF, але в ідеалі їх теж треба в бекграунд
        private readonly YahooFinanceService _yahooFinanceService;
        private readonly RiskEngine _riskEngine;
        private readonly PdfReportService _pdfReportService;
        private readonly AiAnalyticsService _aiAnalyticsService;

        public SimulationController(
            IBackgroundJobClient backgroundJobs,
            IDistributedCache cache,
            YahooFinanceService yahooFinanceService,
            RiskEngine riskEngine,
            PdfReportService pdfReportService,
            AiAnalyticsService aiAnalyticsService,
            ILogger<SimulationController> logger)
        {
            _backgroundJobs = backgroundJobs;
            _cache = cache;
            _yahooFinanceService = yahooFinanceService;
            _riskEngine = riskEngine;
            _pdfReportService = pdfReportService;
            _aiAnalyticsService = aiAnalyticsService;
            _logger = logger;
        }

        [HttpPost("run")]
        public IActionResult RunSimulation([FromBody] SimulationRequestDto dto)
        {
            // Валідація
            if (dto.SimulationsCount <= 0 || dto.Horizon <= 0)
            {
                return BadRequest(new { Message = "Невалідні параметри симуляції" });
            }

            // Ставимо задачу в чергу
            var firebaseUid = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var jobId = _backgroundJobs.Enqueue<ISimulationJob>(job => job.ExecuteAsync(dto, firebaseUid, null));
            _logger.LogInformation("Simulation request enqueued with JobId: {JobId}", jobId);

            // Повертаємо 202 Accepted замість чекання
            return Accepted(new {
                Message = "Simulation started",
                JobId = jobId,
                StatusUrl = $"/api/simulation/status/{jobId}"
            });
        }

        [HttpGet("status/{jobId}")]
        public async Task<IActionResult> GetJobStatus(string jobId)
        {
            var firebaseUid = User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value ?? "anonymous";
            var cachedJson = await _cache.GetStringAsync($"sim_job_{firebaseUid}_{jobId}");
            if (string.IsNullOrEmpty(cachedJson))
            {
                return NotFound(new { Message = "Симуляція не знайдена, ще не почалась, або результат застарів." });
            }

            return Content(cachedJson, "application/json");
        }

        [HttpPost("report")]
        public async Task<IActionResult> GenerateReport([FromBody] SimulationRequestDto dto)
        {
            // Залишаємо поки синхронно, оскільки це PDF-генерація, 
            // хоча в майбутньому варто теж перевести на Hangfire.
            try
            {
                bool isBacktest = dto.Algorithm?.ToLowerInvariant() == "backtest" || dto.IsBacktest;
                var algorithm = ParseAlgorithm(dto.Algorithm);
                
                if (algorithm is null) return BadRequest(new { Message = "Невідомий алгоритм" });
                var scenario = ParseScenario(dto.Scenario);

                var historyResponse = await _yahooFinanceService.GetHistoricalDataAsync(dto.Ticker, dto.LookbackYears);
                if (historyResponse?.data == null || historyResponse.data.Count < 10)
                    return BadRequest(new { Message = "Не вдалося отримати історичні дані." });

                var priceDataPoints = historyResponse.data.Select(h => new PriceDataPoint { Date = h.Date, Price = h.Close }).ToList();

                var simulationResult = _riskEngine.RunSimulation(
                    priceDataPoints, algorithm.Value, dto.SimulationsCount, dto.Horizon, scenario, dto.ConfidenceLevel, dto.CustomShockPercentage ?? 0, isBacktest, dto.RiskFreeRate);

                var news = await _yahooFinanceService.GetAssetNewsAsync(dto.Ticker);
                var aiSummary = await _aiAnalyticsService.GenerateRiskSummaryAsync(dto.Ticker, simulationResult, news);

                var pdfBytes = _pdfReportService.GenerateReport(dto, simulationResult, aiSummary);
                return File(pdfBytes, "application/pdf", $"RiskMate_Report_{dto.Ticker}.pdf");
            }
            catch (System.Exception ex)
            {
                _logger.LogError(ex, "Помилка генерації PDF");
                return StatusCode(500, new { Message = "Помилка генерації звіту." });
            }
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
