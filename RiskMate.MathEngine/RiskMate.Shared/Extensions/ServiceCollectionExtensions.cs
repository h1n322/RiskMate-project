using System;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Polly;
using Polly.Extensions.Http;
using RiskMate.Api.Services;

namespace RiskMate.Shared.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddRiskMateSettings(this IServiceCollection services, Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            services.Configure<RiskMate.Shared.Settings.RiskMateSettings>(configuration.GetSection("RiskMateSettings"));
            return services;
        }
        public static IServiceCollection AddRiskMateServices(this IServiceCollection services)
        {
            services.AddHttpClient<YahooFinanceService>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5)) // avoid socket exhaustion
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddHttpClient<AiAnalyticsService>()
                .SetHandlerLifetime(TimeSpan.FromMinutes(5))
                .AddPolicyHandler(GetRetryPolicy())
                .AddPolicyHandler(GetCircuitBreakerPolicy());

            services.AddSingleton<RiskMate.MathEngine.Simulators.Interfaces.IMonteCarloSimulator, RiskMate.MathEngine.Simulators.MonteCarloSimulator>();
            services.AddSingleton<RiskMate.MathEngine.Simulators.Interfaces.IHistoricalSimulator, RiskMate.MathEngine.Simulators.HistoricalSimulator>();
            services.AddSingleton<RiskMate.MathEngine.Simulators.Interfaces.IGarchSimulator, RiskMate.MathEngine.Simulators.GarchSimulator>();
            services.AddSingleton<RiskMate.MathEngine.Simulators.Interfaces.IMertonJumpSimulator, RiskMate.MathEngine.Simulators.MertonJumpSimulator>();
            services.AddSingleton<RiskMate.MathEngine.Simulators.Interfaces.IStressTestSimulator, RiskMate.MathEngine.Simulators.StressTestSimulator>();
            return services;
        }

        private static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError() // 5xx, 408
                .WaitAndRetryAsync(3, retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))); 
                // 2s, 4s, 8s
        }

        private static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
        {
            return HttpPolicyExtensions
                .HandleTransientHttpError()
                .CircuitBreakerAsync(5, TimeSpan.FromSeconds(30)); 
                // Розриваємо на 30 сек після 5 невдалих спроб підряд
        }
    }
}
