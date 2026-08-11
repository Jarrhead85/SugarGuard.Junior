using System.Globalization;
using System.Text;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация форматирования CSV
/// </summary>
public sealed class CsvExportService : ICsvExportService
{
    /// <inheritdoc/>
    public string BuildMeasurementsCsv(IEnumerable<Measurement> measurements)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MeasurementId,ChildId,MeasurementTime,GlucoseValue,ChildState,Notes,DataSource");

        foreach (var m in measurements)
        {
            sb.Append(m.MeasurementId).Append(',')
              .Append(m.ChildId).Append(',')
              .Append(m.MeasurementTime.ToString("O", CultureInfo.InvariantCulture)).Append(',')
              .Append(m.GlucoseValue.ToString(CultureInfo.InvariantCulture)).Append(',')
              .Append(EscapeCsv(m.ChildState)).Append(',')
              .Append(EscapeCsv(m.Notes)).Append(',')
              .Append(EscapeCsv(m.DataSource))
              .AppendLine();
        }

        return sb.ToString();
    }

    /// <summary>
    /// Экранирует значение ячейки CSV
    /// </summary>
    private static string EscapeCsv(string? value)
        => CsvCellEncoder.Encode(value);
}
