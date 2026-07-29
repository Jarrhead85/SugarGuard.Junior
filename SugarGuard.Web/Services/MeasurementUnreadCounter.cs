using SugarGuard.Web.ViewModels;

namespace SugarGuard.Web.Services;

/// <summary>
/// Рассчитывает количество измерений, поступивших после последнего просмотра.
/// </summary>
public static class MeasurementUnreadCounter
{
    /// <summary>
    /// Возвращает число непросмотренных измерений. Пока пользователь ещё не
    /// открывал журнал, показываются только данные, поступившие в последний час.
    /// </summary>
    public static int Count(
        IEnumerable<MeasurementVm> measurements,
        DateTime? seenAt,
        DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(measurements);

        var baseline = seenAt ?? utcNow.AddHours(-1);
        return measurements.Count(measurement => GetReceivedAt(measurement) > baseline);
    }

    private static DateTime GetReceivedAt(MeasurementVm measurement) =>
        measurement.CreatedAt == default
            ? measurement.MeasurementTime
            : measurement.CreatedAt;
}
