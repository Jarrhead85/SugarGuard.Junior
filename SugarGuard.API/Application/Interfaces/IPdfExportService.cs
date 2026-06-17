using SugarGuard.API.DTOs;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Интерфейс сервиса для экспорта данных в PDF
/// </summary>
public interface IPdfExportService
{
    /// <summary>
    /// Генерирует PDF-отчёт со статистикой измерений
    /// </summary>
    Task<byte[]> GenerateStatisticsReportAsync(StatisticsResponse statistics, string childName);

    /// <summary>
    /// Генерирует PDF-отчёт с подробной таблицей измерений
    /// </summary>
    Task<byte[]> GenerateDetailedReportAsync(StatisticsResponse statistics, string childName);
}
