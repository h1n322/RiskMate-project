using RiskMate.Shared.Extensions;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using StackExchange.Redis;
using RiskMate.Worker.Jobs;
using RiskMate.Shared.Interfaces;
using RiskMate.Api.Services;
using RiskMate.MathEngine;
using RiskMate.MathEngine.Simulators;

var builder = Host.CreateDefaultBuilder(args);

builder.ConfigureServices((hostContext, services) =>
{
    var configuration = hostContext.Configuration;
    
    // Redis Connection
    var redisConnectionString = configuration.GetConnectionString("Redis") ?? "localhost:6379";
    var redis = ConnectionMultiplexer.Connect(redisConnectionString);

    services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisConnectionString;
        options.InstanceName = "RiskMate_";
    });

    // Hangfire Server
    services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UseRedisStorage(redis, new RedisStorageOptions
        {
            Prefix = "hangfire:"
        }));

    services.AddHangfireServer(options => 
    {
        options.WorkerCount = Environment.ProcessorCount * 2;
    });

    // Register Services
    services.AddMemoryCache();
    services.AddRiskMateServices();
    services.AddSingleton<RiskEngine>();
    services.AddSingleton<BacktestSimulator>();
    
    // Register Jobs
    services.AddTransient<ISimulationJob, SimulationJob>();
});

var host = builder.Build();
host.Run();
