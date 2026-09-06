using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using RiskMate.Api.DTOs;
using RiskMate.Shared.Settings;
using System.Collections.Generic;
using System;
using System.Threading.Tasks;
using System.Linq;
using System.Net.Http;

namespace RiskMate.Api.Services
{
    public class HistoricalPriceDto
    {
        public DateTime Date { get; set; }
        public double Close { get; set; }
    }

    public class HistoryResponseDto
    {
        public bool is_mock { get; set; }
        public List<HistoricalPriceDto> data { get; set; }
    }

    public class YahooFinanceService
    {
        private readonly HttpClient _httpClient;
        private readonly IDistributedCache _cache;
        private readonly RiskMateSettings _settings;

        public YahooFinanceService(HttpClient httpClient, IDistributedCache cache, IOptions<RiskMateSettings> settings)
        {
            _httpClient = httpClient;
            _cache = cache;
            _settings = settings.Value;
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "RiskMate C# Backend");
        }

        public async Task<HistoryResponseDto> GetHistoricalDataAsync(string ticker, int lookbackYears = 5)
        {
            var cacheKey = $"history_response_{ticker}_{lookbackYears}";
            var cachedJson = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedData = JsonSerializer.Deserialize<HistoryResponseDto>(cachedJson);
                if (cachedData != null) return cachedData;
            }

            var baseUrl = _settings.PythonApiUrl.TrimEnd('/');
            var url = $"{baseUrl}/api/history/{ticker}?lookback={lookbackYears}";
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();

            var jsonString = await response.Content.ReadAsStringAsync();
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var data = JsonSerializer.Deserialize<HistoryResponseDto>(jsonString, options) 
                       ?? new HistoryResponseDto { data = new List<HistoricalPriceDto>() };

            var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(2) };
            await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(data), cacheOptions);

            return data;
        }

        public async Task<List<double>> GetHistoricalPricesAsync(string ticker, int lookbackYears = 5)
        {
            var resp = await GetHistoricalDataAsync(ticker, lookbackYears);
            return resp.data.Select(d => d.Close).ToList();
        }

        public async Task<List<NewsItemDto>> GetAssetNewsAsync(string ticker, int count = 5)
        {
            var cacheKey = $"news_{ticker}_{count}";
            var cachedJson = await _cache.GetStringAsync(cacheKey);

            if (!string.IsNullOrEmpty(cachedJson))
            {
                var cachedNews = JsonSerializer.Deserialize<List<NewsItemDto>>(cachedJson);
                if (cachedNews != null) return cachedNews;
            }

            try
            {
                var baseUrl = _settings.PythonApiUrl.TrimEnd('/');
                var url = $"{baseUrl}/api/news/{ticker}?limit={count}";
                var response = await _httpClient.GetAsync(url);
                response.EnsureSuccessStatusCode();

                var jsonString = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var data = JsonSerializer.Deserialize<List<NewsItemDto>>(jsonString, options) 
                           ?? new List<NewsItemDto>();

                var cacheOptions = new DistributedCacheEntryOptions { AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30) };
                await _cache.SetStringAsync(cacheKey, JsonSerializer.Serialize(data), cacheOptions);

                return data;
            }
            catch (Exception)
            {
                return new List<NewsItemDto>();
            }
        }
    }
}
