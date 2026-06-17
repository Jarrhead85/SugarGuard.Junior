using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Models;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Сервис для определения статуса уровня глюкозы
/// </summary>
public class GlucoseStatusService : IGlucoseStatusService
{
    /// <inheritdoc />
    public string GetGlucoseStatus(decimal glucoseValue)
    {
        var g = (double)glucoseValue;
        var status = g switch
        {
            <= GlucoseLevels.CriticallyLowThreshold => GlucoseStatus.CriticallyLow,
            <= GlucoseLevels.LowThreshold => GlucoseStatus.Low,
            <= GlucoseLevels.TargetRangeMax => GlucoseStatus.Normal,
            <= GlucoseLevels.CriticallyHighThreshold => GlucoseStatus.High,
            _ => GlucoseStatus.CriticallyHigh
        };
        return status.ToString();
    }

    /// <inheritdoc />
    public bool IsCritical(decimal glucoseValue)
    {
        return GlucoseLevels.IsCritical((double)glucoseValue);
    }

    /// <inheritdoc />
    public string GetStatusDescription(string status)
    {
        return status switch
        {
            "CriticallyLow" => "Критически низкий",
            "Low" => "Низкий",
            "Normal" => "Норма",
            "High" => "Высокий",
            "CriticallyHigh" => "Критически высокий",
            _ => "Неизвестно"
        };
    }
}
