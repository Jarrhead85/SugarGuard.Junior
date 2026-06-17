using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Константы оформления PDF по умолчанию
/// </summary>
internal static class PdfDefaults
{
    public const string FontFamily = "Arial";
    public const int BaseFontSize = 12;
    public const int SmallFontSize = 10;
    public const float PageMarginCm = 2f;
    public const float PageMarginCmDetailed = 1.5f;
}

/// <summary>
/// Сервис для генерации PDF-отчётов со статистикой измерений глюкозы
/// </summary>
public class PdfExportService : IPdfExportService
{
    private readonly ILogger<PdfExportService> _logger;

    public PdfExportService(ILogger<PdfExportService> logger)
    {
        _logger = logger;        
        QuestPDF.Settings.License = LicenseType.Community;
    }

    /// <summary>
    /// Генерирует PDF-отчёт со статистикой измерений
    /// </summary>
    public async Task<byte[]> GenerateStatisticsReportAsync(StatisticsResponse statistics, string childName)
    {
        try
        {
            _logger.LogInformation("Генерация PDF-отчёта для ребёнка {ChildName}, период {Period}", 
                childName, statistics.Period);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(PdfDefaults.PageMarginCm, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(PdfDefaults.BaseFontSize).FontFamily(PdfDefaults.FontFamily));

                    page.Header()
                        .Text($"Отчёт по глюкозе - {childName}")
                        .SemiBold().FontSize(20).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(1, Unit.Centimetre)
                        .Column(column =>
                        {
                            // Информация о периоде
                            column.Item().Element(ComposeReportInfo);

                            column.Item().PaddingTop(20).Element(ComposeStatisticsTable);

                            if (statistics.Measurements.Any())
                            {
                                column.Item().PaddingTop(20).Element(ComposeGlucoseChart);
                            }

                            // Таблица последних измерений
                            if (statistics.Measurements.Any())
                            {
                                column.Item().PaddingTop(20).Element(ComposeMeasurementsTable);
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Сгенерировано: ");
                            x.Span($"{DateTime.Now:dd.MM.yyyy HH:mm}").SemiBold();
                            x.Span(" | SugarGuard Junior");
                        });
                });
            });

            var pdfBytes = document.GeneratePdf();
            
            _logger.LogInformation("PDF-отчёт успешно сгенерирован, размер: {Size} байт", pdfBytes.Length);
            
            return await Task.FromResult(pdfBytes);

            void ComposeReportInfo(IContainer container)
            {
                container.Background(Colors.Grey.Lighten3).Padding(15).Column(column =>
                {
                    column.Item().Text($"Период: {statistics.Period}").FontSize(14).SemiBold();
                    column.Item().Text($"С {statistics.FromDate:dd.MM.yyyy} по {statistics.ToDate:dd.MM.yyyy}");
                    column.Item().Text($"Всего измерений: {statistics.TotalMeasurements}");
                });
            }

            void ComposeStatisticsTable(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().PaddingBottom(10).Text("Статистические показатели").FontSize(16).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                        });

                        // Заголовок
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Показатель").SemiBold();
                            header.Cell().Element(CellStyle).Text("Значение").SemiBold();
                            header.Cell().Element(CellStyle).Text("Норма").SemiBold();
                        });

                        // Данные
                        table.Cell().Element(CellStyle).Text("Среднее значение");
                        table.Cell().Element(CellStyle).Text($"{statistics.AverageGlucose:F1} ммоль/л");
                        table.Cell().Element(CellStyle).Text("4.0-10.0");

                        table.Cell().Element(CellStyle).Text("Минимум");
                        table.Cell().Element(CellStyle).Text($"{statistics.MinGlucose:F1} ммоль/л");
                        table.Cell().Element(CellStyle).Text("-");

                        table.Cell().Element(CellStyle).Text("Максимум");
                        table.Cell().Element(CellStyle).Text($"{statistics.MaxGlucose:F1} ммоль/л");
                        table.Cell().Element(CellStyle).Text("-");

                        table.Cell().Element(CellStyle).Text("Время в целевом диапазоне");
                        table.Cell().Element(CellStyle).Text($"{statistics.TimeInTargetRange:F1}%");
                        table.Cell().Element(CellStyle).Text(">70%");

                        table.Cell().Element(CellStyle).Text("Стандартное отклонение");
                        table.Cell().Element(CellStyle).Text($"{statistics.StandardDeviation:F1}");
                        table.Cell().Element(CellStyle).Text("<2.0");

                        table.Cell().Element(CellStyle).Text("Гипогликемии (<4.0)");
                        table.Cell().Element(CellStyle).Text($"{statistics.HypoEpisodes}");
                        table.Cell().Element(CellStyle).Text("0");

                        table.Cell().Element(CellStyle).Text("Гипергликемии (>10.0)");
                        table.Cell().Element(CellStyle).Text($"{statistics.HyperEpisodes}");
                        table.Cell().Element(CellStyle).Text("0");

                        table.Cell().Element(CellStyle).Text("Критические эпизоды");
                        table.Cell().Element(CellStyle).Text($"{statistics.CriticalEpisodes}");
                        table.Cell().Element(CellStyle).Text("0");
                    });
                });
            }

            void ComposeGlucoseChart(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().PaddingBottom(10).Text("График уровня глюкозы").FontSize(16).SemiBold();
                    
                    // Простой текстовый график
                    column.Item().Background(Colors.Grey.Lighten4).Padding(20).Column(chartColumn =>
                    {
                        chartColumn.Item().PaddingBottom(10).Text("Последние 10 измерений:").SemiBold();
                        
                        var recentMeasurements = statistics.Measurements
                            .OrderBy(m => m.MeasurementTime)
                            .TakeLast(10)
                            .ToList();

                        foreach (var measurement in recentMeasurements)
                        {
                            var status = GetStatusIcon(measurement.GlucoseValue);
                            chartColumn.Item().Text($"{measurement.MeasurementTime:dd.MM HH:mm} - {measurement.GlucoseValue:F1} ммоль/л {status}");
                        }
                    });
                });
            }

            void ComposeMeasurementsTable(IContainer container)
            {
                container.Column(column =>
                {
                    column.Item().PaddingBottom(10).Text("Последние измерения").FontSize(16).SemiBold();

                    column.Item().Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn(2);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(1);
                            columns.RelativeColumn(2);
                        });

                        // Заголовок
                        table.Header(header =>
                        {
                            header.Cell().Element(CellStyle).Text("Дата и время").SemiBold();
                            header.Cell().Element(CellStyle).Text("Глюкоза").SemiBold();
                            header.Cell().Element(CellStyle).Text("Статус").SemiBold();
                            header.Cell().Element(CellStyle).Text("Заметки").SemiBold();
                        });

                        // Данные
                        var recentMeasurements = statistics.Measurements
                            .OrderByDescending(m => m.MeasurementTime)
                            .Take(20);

                        foreach (var measurement in recentMeasurements)
                        {
                            table.Cell().Element(CellStyle).Text($"{measurement.MeasurementTime:dd.MM.yyyy HH:mm}");
                            table.Cell().Element(CellStyle).Text($"{measurement.GlucoseValue:F1}");
                            table.Cell().Element(CellStyle).Text(GetStatusText(measurement.GlucoseValue));
                            table.Cell().Element(CellStyle).Text(measurement.Notes ?? "-");
                        }
                    });
                });
            }

            static IContainer CellStyle(IContainer container)
            {
                return container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(5);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при генерации PDF-отчёта для ребёнка {ChildName}", childName);
            throw;
        }
    }

    /// <summary>
    /// Генерирует подробный PDF-отчёт с полной таблицей измерений
    /// </summary>
    public async Task<byte[]> GenerateDetailedReportAsync(StatisticsResponse statistics, string childName)
    {
        try
        {
            _logger.LogInformation("Генерация подробного PDF-отчёта для ребёнка {ChildName}, измерений: {Count}", 
                childName, statistics.Measurements.Count);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(PdfDefaults.PageMarginCmDetailed, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(PdfDefaults.SmallFontSize).FontFamily(PdfDefaults.FontFamily));

                    page.Header()
                        .Text($"Подробный отчёт - {childName} ({statistics.Period})")
                        .SemiBold().FontSize(16).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(0.5f, Unit.Centimetre)
                        .Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(2);
                            });

                            // Заголовок таблицы
                            table.Header(header =>
                            {
                                header.Cell().Element(HeaderCellStyle).Text("Дата и время").SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Глюкоза").SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Статус").SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Состояние").SemiBold();
                                header.Cell().Element(HeaderCellStyle).Text("Заметки").SemiBold();
                            });

                            // Все измерения
                            var sortedMeasurements = statistics.Measurements
                                .OrderByDescending(m => m.MeasurementTime);

                            foreach (var measurement in sortedMeasurements)
                            {
                                table.Cell().Element(DataCellStyle).Text($"{measurement.MeasurementTime:dd.MM.yyyy HH:mm}");
                                table.Cell().Element(DataCellStyle).Text($"{measurement.GlucoseValue:F1}");
                                table.Cell().Element(DataCellStyle).Text(GetStatusText(measurement.GlucoseValue));
                                table.Cell().Element(DataCellStyle).Text(measurement.ChildState ?? "-");
                                table.Cell().Element(DataCellStyle).Text(measurement.Notes ?? "-");
                            }
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.CurrentPageNumber();
                            x.Span(" / ");
                            x.TotalPages();
                            x.Span($" | Сгенерировано: {DateTime.Now:dd.MM.yyyy HH:mm}");
                        });
                });
            });

            var pdfBytes = document.GeneratePdf();
            
            _logger.LogInformation("Подробный PDF-отчёт успешно сгенерирован, размер: {Size} байт", pdfBytes.Length);
            
            return await Task.FromResult(pdfBytes);

            static IContainer HeaderCellStyle(IContainer container)
            {
                return container
                    .Background(Colors.Blue.Lighten4)
                    .Border(1)
                    .BorderColor(Colors.Grey.Medium)
                    .PaddingVertical(8)
                    .PaddingHorizontal(5)
                    .AlignCenter();
            }

            static IContainer DataCellStyle(IContainer container)
            {
                return container
                    .Border(1)
                    .BorderColor(Colors.Grey.Lighten1)
                    .PaddingVertical(5)
                    .PaddingHorizontal(5);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при генерации подробного PDF-отчёта для ребёнка {ChildName}", childName);
            throw;
        }
    }

    /// <summary>
    /// Возвращает текстовое описание статуса глюкозы
    /// </summary>
    private static string GetStatusText(decimal glucoseValue)
    {
        var glucose = (double)glucoseValue;
        return glucose switch
        {
            < 3.1 => "КРИТИЧЕСКИ НИЗКО",
            < 4.0 => "НИЗКО",
            <= 10.0 => "НОРМА",
            <= 15.0 => "ВЫСОКО",
            _ => "КРИТИЧЕСКИ ВЫСОКО"
        };
    }

    /// <summary>
    /// Возвращает иконку для статуса глюкозы
    /// </summary>
    private static string GetStatusIcon(decimal glucoseValue)
    {
        var glucose = (double)glucoseValue;
        return glucose switch
        {
            < 3.1 => "⚠️",
            < 4.0 => "⬇️",
            <= 10.0 => "✅",
            <= 15.0 => "⬆️",
            _ => "🚨"
        };
    }
}
