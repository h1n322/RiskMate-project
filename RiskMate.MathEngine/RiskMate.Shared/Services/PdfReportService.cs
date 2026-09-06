using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using RiskMate.MathEngine.Models;
using RiskMate.Api.DTOs;
using ScottPlot;

namespace RiskMate.Api.Services
{
    public class PdfReportService
    {
        public PdfReportService()
        {
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public byte[] GenerateReport(SimulationRequestDto request, SimulationResult result, string? aiSummary = null)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(11));

                    page.Header().Element(ComposeHeader);
                    page.Content().Element(c => ComposeContent(c, request, result, aiSummary));
                    page.Footer().Element(ComposeFooter);
                });
            });

            return document.GeneratePdf();
        }

        private byte[] GenerateChartImage(SimulationResult result)
        {
            var plt = new Plot();
            plt.Title("Прогноз та історія ціни");
            plt.XLabel("Дні");
            plt.YLabel("Ціна ($)");

            var historyPoints = result.ChartPoints.Where(p => p.History.HasValue).Select(p => p.History.Value).ToArray();
            int offset = historyPoints.Length > 0 ? historyPoints.Length - 1 : 0;

            if (historyPoints.Length > 0)
            {
                var sigHist = plt.Add.Signal(historyPoints);
                sigHist.LegendText = "Історія";
                sigHist.Color = ScottPlot.Colors.Green;
            }

            var forecastPoints = result.ChartPoints.Where(p => p.Forecast.HasValue).Select(p => p.Forecast.Value).ToArray();
            if (forecastPoints.Length > 0)
            {
                var sigFore = plt.Add.Signal(forecastPoints);
                sigFore.LegendText = "Прогноз (Очікуваний)";
                sigFore.Color = ScottPlot.Colors.Blue;
                sigFore.Data.XOffset = offset;
            }

            var actualPoints = result.ChartPoints.Where(p => p.Actual.HasValue).Select(p => p.Actual.Value).ToArray();
            if (actualPoints.Length > 0)
            {
                var sigAct = plt.Add.Signal(actualPoints);
                sigAct.LegendText = "Реальність (Бектест)";
                sigAct.Color = ScottPlot.Colors.Red;
                sigAct.Data.XOffset = offset;
            }

            var lowerPoints = result.ChartPoints.Where(p => p.LowerBound.HasValue).Select(p => p.LowerBound.Value).ToArray();
            if (lowerPoints.Length > 0)
            {
                var sigLow = plt.Add.Signal(lowerPoints);
                sigLow.LegendText = "Песимістичний сценарій";
                sigLow.Color = ScottPlot.Colors.Gray;
                sigLow.Data.XOffset = offset;
                sigLow.LinePattern = LinePattern.Dashed;
            }

            var upperPoints = result.ChartPoints.Where(p => p.UpperBound.HasValue).Select(p => p.UpperBound.Value).ToArray();
            if (upperPoints.Length > 0)
            {
                var sigUp = plt.Add.Signal(upperPoints);
                sigUp.LegendText = "Оптимістичний сценарій";
                sigUp.Color = ScottPlot.Colors.Gray;
                sigUp.Data.XOffset = offset;
                sigUp.LinePattern = LinePattern.Dashed;
            }

            plt.ShowLegend();
            return plt.GetImageBytes(800, 350, ScottPlot.ImageFormat.Png);
        }

        private void ComposeHeader(IContainer container)
        {
            container.Row(row =>
            {
                row.RelativeItem().Column(column =>
                {
                    column.Item().Text("RiskMate AI").FontSize(24).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    column.Item().Text("Звіт про фінансовий ризик").FontSize(14).FontColor(QuestPDF.Helpers.Colors.Grey.Medium);
                });
                row.ConstantItem(100).AlignRight().Text($"{DateTime.Now:dd.MM.yyyy HH:mm}");
            });
        }

        private void ComposeContent(IContainer container, SimulationRequestDto request, SimulationResult result, string aiSummary)
        {
            container.PaddingVertical(1, Unit.Centimetre).Column(column =>
            {
                column.Spacing(20);

                column.Item().Text("Параметри симуляції").FontSize(16).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                
                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Text("Актив (Тикер):");
                    table.Cell().Text(request.Ticker).SemiBold();

                    table.Cell().Text("Алгоритм:");
                    table.Cell().Text(request.Algorithm ?? "gbm").SemiBold();

                    table.Cell().Text("Сценарій стрес-тесту:");
                    table.Cell().Text(string.IsNullOrEmpty(request.Scenario) ? "Base" : request.Scenario).SemiBold();

                    table.Cell().Text("Кількість симуляцій:");
                    table.Cell().Text(request.SimulationsCount.ToString()).SemiBold();

                    table.Cell().Text("Горизонт (днів):");
                    table.Cell().Text(request.Horizon.ToString()).SemiBold();
                    
                    table.Cell().Text("Рівень довіри (VaR):");
                    table.Cell().Text($"{request.ConfidenceLevel * 100}%").SemiBold();

                    table.Cell().Text("Глибина історії (років):");
                    table.Cell().Text(request.LookbackYears.ToString()).SemiBold();

                    table.Cell().Text("Безризикова ставка:");
                    table.Cell().Text($"{(request.RiskFreeRate * 100):F2}%").SemiBold();
                });

                column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);

                column.Item().Text("Результати аналізу").FontSize(16).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);

                column.Item().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn();
                        columns.RelativeColumn();
                    });

                    table.Cell().Text("Очікувана ціна:");
                    table.Cell().Text($"${result.ExpectedPrice:F2}").SemiBold().FontColor(QuestPDF.Helpers.Colors.Green.Darken2);

                    table.Cell().Text("Value at Risk (VaR):");
                    table.Cell().Text($"${result.ValueAtRisk:F2}").SemiBold().FontColor(QuestPDF.Helpers.Colors.Red.Darken2);

                    table.Cell().Text("Conditional VaR (CVaR):");
                    table.Cell().Text($"${result.ConditionalValueAtRisk:F2}").SemiBold().FontColor(QuestPDF.Helpers.Colors.Red.Darken2);

                    table.Cell().Text("Волатильність:");
                    table.Cell().Text($"{result.Volatility:F2}%").SemiBold();

                    table.Cell().Text("Коефіцієнт Шарпа:");
                    table.Cell().Text($"{result.SharpeRatio:F2}").SemiBold();
                });

                if (result.Hedging != null)
                {
                    column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                    column.Item().Text("Стратегія Хеджування (Black-Scholes)").FontSize(16).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                    
                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Cell().Text("Тип опціону:");
                        table.Cell().Text("Put-опціон").SemiBold();
                        
                        table.Cell().Text("Страйк-ціна:");
                        table.Cell().Text($"${result.Hedging.StrikePrice:F2}").SemiBold();
                        
                        table.Cell().Text("Премія за опціон (за 1 акцію):");
                        table.Cell().Text($"${result.Hedging.PutOptionPremium:F2}").SemiBold().FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                        
                        table.Cell().Text("Вартість хеджу 100 акцій:");
                        table.Cell().Text($"${result.Hedging.TotalCostFor100Shares:F2}").SemiBold().FontColor(QuestPDF.Helpers.Colors.Red.Medium);

                        table.Cell().Text("Термін дії:");
                        table.Cell().Text(result.Hedging.Expiration).SemiBold();
                    });
                }
                
                column.Item().LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                
                column.Item().Text("Графік прогнозу").FontSize(16).SemiBold().FontColor(QuestPDF.Helpers.Colors.Blue.Darken2);
                
                try
                {
                    byte[] chartBytes = GenerateChartImage(result);
                    column.Item().Image(chartBytes);
                }
                catch (Exception ex)
                {
                    column.Item().Text("Не вдалося згенерувати графік.").FontColor(QuestPDF.Helpers.Colors.Red.Medium);
                }

                if (!string.IsNullOrEmpty(aiSummary))
                {
                    column.Item().PaddingTop(10).LineHorizontal(1).LineColor(QuestPDF.Helpers.Colors.Grey.Lighten2);
                    column.Item().PaddingTop(10).Text("AI Аналітика 🧠").FontSize(16).SemiBold().FontColor(QuestPDF.Helpers.Colors.Purple.Darken2);
                    column.Item().Text(aiSummary).FontSize(11).FontColor(QuestPDF.Helpers.Colors.Black);
                }

                column.Item().PaddingTop(20).Text("Цей звіт згенеровано автоматично ядромі відображає ймовірнісні показники ризику. Не є фінансовою рекомендацією.").FontSize(9).FontColor(QuestPDF.Helpers.Colors.Grey.Medium).Italic();
            });
        }

        private void ComposeFooter(IContainer container)
        {
            container.AlignCenter().Text(x =>
            {
                x.Span("Сторінка ");
                x.CurrentPageNumber();
                x.Span(" з ");
                x.TotalPages();
            });
        }
    }
}
