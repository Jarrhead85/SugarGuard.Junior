using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Инкапсулирует форматирование CSV
/// </summary>
public interface ICsvExportService
{   
    string BuildMeasurementsCsv(IEnumerable<Measurement> measurements); // Формирует CSV-строку из списка замеров глюкозы
}
