using RiskMate.Shared.Extensions;
using RiskMate.Api.Middlewares;
using Serilog;
using Microsoft.EntityFrameworkCore;
using RiskMate.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using RiskMate.Api.Models;
using RiskMate.Api.Services;
using RiskMate.MathEngine;
using RiskMate.MathEngine.Simulators;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using Hangfire;
using Hangfire.Redis.StackExchange;
using StackExchange.Redis;


Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting up RiskMate API...");
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext()
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}"));


builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

var firebaseProjectId = builder.Configuration["Firebase:ProjectId"];
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://securetoken.google.com/{firebaseProjectId}";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://securetoken.google.com/{firebaseProjectId}",
            ValidateAudience = true,
            ValidAudience = firebaseProjectId,
            ValidateLifetime = true
        };
    });

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins("http://localhost:5173", "http://localhost:5174", "http://127.0.0.1:5173", "http://127.0.0.1:5174", "http://localhost:3000")
              .SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddAuthorization();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddMemoryCache();
builder.Services.AddControllers();
builder.Services.AddRiskMateSettings(builder.Configuration);
builder.Services.AddRiskMateServices();
builder.Services.AddSingleton<RiskEngine>();
builder.Services.AddSingleton<BacktestSimulator>();
builder.Services.AddSingleton<PdfReportService>();
var redisConnectionString = builder.Configuration.GetConnectionString("Redis") ?? "localhost:6379";
var redis = ConnectionMultiplexer.Connect(redisConnectionString);
builder.Services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = redisConnectionString;
    options.InstanceName = "RiskMate_";
});
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseRedisStorage(redis, new RedisStorageOptions
    {
        Prefix = "hangfire:"
    }));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = 429;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
    {
        var userId = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
        var partitionKey = userId ?? httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(partitionKey, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 2
        });
    });
});
var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    dbContext.Database.Migrate();
}


app.UseMiddleware<GlobalExceptionMiddleware>();
app.UseMiddleware<RequestLoggingMiddleware>();
app.UseCors("AllowReactApp");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHangfireDashboard("/hangfire");
app.UseAuthentication();      
app.UseAuthorization();       
app.MapControllers();

app.MapGet("/", () => "RiskMate API is running!");

app.MapPost("/api/auth/sync", async (AppDbContext db, HttpContext httpContext) =>
{
    var firebaseUid = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
    var email = httpContext.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value ??
                httpContext.User.Claims.FirstOrDefault(c => c.Type == "email")?.Value;

    if (string.IsNullOrEmpty(firebaseUid))
        return Results.Unauthorized();

    var user = await db.Users.FirstOrDefaultAsync(u => u.FirebaseUid == firebaseUid);

    if (user == null)
    {
        user = new User
        {
            FirebaseUid = firebaseUid,
            Email = email ?? "no-email@provided.com",
            CreatedAt = DateTime.UtcNow
        };
        
        db.Users.Add(user);
        await db.SaveChangesAsync();
        
        return Results.Ok(new { Message = "Новий користувач успішно створений у БД", User = user });
    }

    return Results.Ok(new { Message = "Користувач вже існує", User = user });
}).RequireAuthorization();


    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Unhandled exception during application startup.");
}
finally
{
    Log.Information("Shut down complete.");
    Log.CloseAndFlush();
}
