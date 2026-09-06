namespace RiskMate.Api.DTOs
{
    public class NewsItemDto
    {
        public string Title { get; set; } = string.Empty;
        public string Link { get; set; } = string.Empty;
        public string Publisher { get; set; } = string.Empty;
        public long Timestamp { get; set; }
    }
}
