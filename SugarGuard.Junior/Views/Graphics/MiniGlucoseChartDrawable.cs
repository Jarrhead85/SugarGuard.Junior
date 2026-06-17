using Microsoft.Maui.Graphics;
using SugarGuard.Shared.Constants;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Views.Graphics;

/// <summary>
/// IDrawable для мини-графика глюкозы на главном экране.
/// Упрощённая версия ChartDrawable: без подписей осей, без крупных точек,
/// адаптирована под высоту ~80–100dp.
/// </summary>
public class MiniGlucoseChartDrawable : IDrawable
{
    // ========== ЦВЕТА (из Colors.xaml / UI Kit) ==========
    private static readonly Color ColorSuccess = Color.FromArgb("37A563");
    private static readonly Color ColorWarning = Color.FromArgb("E3A32B");
    private static readonly Color ColorDanger = Color.FromArgb("DB5967");
    private static readonly Color ColorPrimary = Color.FromArgb("1B8E8B");

    // ========== ДАННЫЕ ==========
    private IReadOnlyList<MiniChartDataPoint> _dataPoints = Array.Empty<MiniChartDataPoint>();

    /// <summary>
    /// Точки графика. При установке вызывает перерисовку через InvalidateCallback.
    /// </summary>
    public IReadOnlyList<MiniChartDataPoint> DataPoints
    {
        get => _dataPoints;
        set
        {
            _dataPoints = value;
            InvalidateCallback?.Invoke();
        }
    }

    /// <summary>
    /// Привязывается к GraphicsView.Invalidate() в code-behind.
    /// </summary>
    public Action? InvalidateCallback { get; set; }

    // ========== IDrawable ==========

    /// <inheritdoc/>
    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        var points = _dataPoints;

        // Нужно минимум 2 точки для отрисовки линии
        if (points.Count < 2)
        {
            DrawEmptyState(canvas, dirtyRect);
            return;
        }

        // Отступы: горизонтальный чуть больше, чтобы крайние точки не обрезались
        const float paddingH = 8f;
        const float paddingV = 6f;

        float chartLeft = dirtyRect.Left + paddingH;
        float chartRight = dirtyRect.Right - paddingH;
        float chartTop = dirtyRect.Top + paddingV;
        float chartBottom = dirtyRect.Bottom - paddingV;
        float chartWidth = chartRight - chartLeft;
        float chartHeight = chartBottom - chartTop;

        // ── Диапазон Y ──
        // ИСПРАВЛЕНИЕ: GlucoseValue — double, GlucoseLevels.TargetRangeMin/Max — double.
        // Расширяем диапазон на ±0.5 за пределы целевой зоны и реальных данных.
        double yMin = Math.Min(points.Min(p => p.GlucoseValue), GlucoseLevels.TargetRangeMin - 0.5);
        double yMax = Math.Max(points.Max(p => p.GlucoseValue), GlucoseLevels.TargetRangeMax + 0.5);
        double yRange = yMax - yMin;
        if (yRange < 0.01) yRange = 1.0; // защита от деления на ноль

        // ── Диапазон X ──
        long xMinTicks = points.Min(p => p.Timestamp.Ticks);
        long xMaxTicks = points.Max(p => p.Timestamp.Ticks);
        long xRange = xMaxTicks - xMinTicks;
        if (xRange == 0) xRange = 1;

        // ── Вспомогательные функции преобразования координат ──
        // ToY: double → float. Все вычисления в double, приводим в float только результат.
        float ToX(DateTime t)
            => chartLeft + (float)((double)(t.Ticks - xMinTicks) / xRange * chartWidth);

        float ToY(double v)
            => chartBottom - (float)((v - yMin) / yRange * chartHeight);

        // ── Слои рисования ──
        DrawAreaFill(canvas, points, chartBottom, ToX, ToY);
        DrawReferenceLines(canvas, chartLeft, chartRight, ToY);
        DrawPolyline(canvas, points, ToX, ToY);
        DrawLastPoint(canvas, points, ToX, ToY);
    }

    // ========== СЛОИ ==========

    /// <summary>
    /// Заливка области под линией (градиент прозрачности).
    /// </summary>
    private static void DrawAreaFill(
        ICanvas canvas,
        IReadOnlyList<MiniChartDataPoint> points,
        float chartBottom,
        Func<DateTime, float> toX,
        Func<double, float> toY)      // ИСПРАВЛЕНИЕ: параметр double, а не decimal
    {
        canvas.SaveState();

        var fillPath = new PathF();
        // Начинаем снизу-слева, поднимаемся к первой точке
        fillPath.MoveTo(toX(points[0].Timestamp), chartBottom);
        fillPath.LineTo(toX(points[0].Timestamp), toY(points[0].GlucoseValue)); // ИСПРАВЛЕНИЕ: double

        for (int i = 1; i < points.Count; i++)
            fillPath.LineTo(toX(points[i].Timestamp), toY(points[i].GlucoseValue)); // ИСПРАВЛЕНИЕ: double

        // Закрываем контур снизу
        fillPath.LineTo(toX(points[^1].Timestamp), chartBottom);
        fillPath.Close();

        canvas.FillColor = ColorPrimary.WithAlpha(0.08f);
        canvas.FillPath(fillPath);

        canvas.RestoreState();
    }

    /// <summary>
    /// Пунктирные горизонтальные линии целевого диапазона.
    /// </summary>
    private static void DrawReferenceLines(
        ICanvas canvas,
        float chartLeft,
        float chartRight,
        Func<double, float> toY)      // ИСПРАВЛЕНИЕ: параметр double, а не decimal
    {
        canvas.SaveState();
        canvas.StrokeSize = 1f;
        canvas.StrokeDashPattern = new float[] { 4f, 3f };

        // Нижняя граница нормы (зелёная)
        canvas.StrokeColor = ColorSuccess.WithAlpha(0.45f);
        float yMin = toY(GlucoseLevels.TargetRangeMin);   // ИСПРАВЛЕНИЕ: double
        canvas.DrawLine(chartLeft, yMin, chartRight, yMin);

        // Верхняя граница нормы (жёлтая)
        canvas.StrokeColor = ColorWarning.WithAlpha(0.45f);
        float yMax = toY(GlucoseLevels.TargetRangeMax);   // ИСПРАВЛЕНИЕ: double
        canvas.DrawLine(chartLeft, yMax, chartRight, yMax);

        canvas.RestoreState();
    }

    /// <summary>
    /// Ломаная линия графика, цвет зависит от последнего состояния.
    /// </summary>
    private static void DrawPolyline(
        ICanvas canvas,
        IReadOnlyList<MiniChartDataPoint> points,
        Func<DateTime, float> toX,
        Func<double, float> toY)      // ИСПРАВЛЕНИЕ: параметр double, а не decimal
    {
        canvas.SaveState();

        // Цвет линии определяем по последней точке
        canvas.StrokeColor = GetStateColorFromValue(points[^1].GlucoseValue);
        canvas.StrokeSize = 2f;
        canvas.StrokeDashPattern = null;

        var path = new PathF();
        path.MoveTo(toX(points[0].Timestamp), toY(points[0].GlucoseValue)); // ИСПРАВЛЕНИЕ: double

        for (int i = 1; i < points.Count; i++)
            path.LineTo(toX(points[i].Timestamp), toY(points[i].GlucoseValue)); // ИСПРАВЛЕНИЕ: double

        canvas.DrawPath(path);
        canvas.RestoreState();
    }

    /// <summary>
    /// Акцентная точка на последнем измерении.
    /// </summary>
    private static void DrawLastPoint(
        ICanvas canvas,
        IReadOnlyList<MiniChartDataPoint> points,
        Func<DateTime, float> toX,
        Func<double, float> toY)      // ИСПРАВЛЕНИЕ: параметр double, а не decimal
    {
        var last = points[^1];
        float cx = toX(last.Timestamp);
        float cy = toY(last.GlucoseValue);         // ИСПРАВЛЕНИЕ: double
        Color stateColor = GetStateColorFromValue(last.GlucoseValue);

        canvas.SaveState();

        // Внешний полупрозрачный ореол
        canvas.FillColor = stateColor.WithAlpha(0.18f);
        canvas.FillCircle(cx, cy, 7f);

        // Центральная точка
        canvas.FillColor = stateColor;
        canvas.FillCircle(cx, cy, 4f);

        canvas.RestoreState();
    }

    /// <summary>
    /// Заглушка при отсутствии данных: пунктирная горизонтальная линия по центру.
    /// </summary>
    private static void DrawEmptyState(ICanvas canvas, RectF dirtyRect)
    {
        canvas.SaveState();

        // ИСПРАВЛЕНИЕ: RectF в MAUI не имеет свойства MidY.
        // Вычисляем середину вручную.
        float cy = (dirtyRect.Top + dirtyRect.Bottom) / 2f;

        canvas.StrokeColor = ColorPrimary.WithAlpha(0.2f);
        canvas.StrokeSize = 1.5f;
        canvas.StrokeDashPattern = new float[] { 5f, 4f };
        canvas.DrawLine(dirtyRect.Left + 8f, cy, dirtyRect.Right - 8f, cy);

        canvas.RestoreState();
    }

    // ========== ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ ==========

    /// <summary>
    /// Определяет цвет по значению глюкозы, используя пороги из GlucoseLevels.
    /// </summary>
    private static Color GetStateColorFromValue(double glucoseValue)
    {
        // Критически низкий или критически высокий
        if (glucoseValue < GlucoseLevels.CriticallyLowThreshold || glucoseValue > GlucoseLevels.CriticallyHighThreshold)
            return ColorDanger;

        // Низкий или высокий (предупреждение)
        if (glucoseValue < GlucoseLevels.TargetRangeMin || glucoseValue > GlucoseLevels.TargetRangeMax)
            return ColorWarning;

        // Норма
        return ColorSuccess;
    }

    /// <summary>
    /// Перегрузка по enum — для совместимости, если где-то передаётся GlucoseUiState.
    /// </summary>
    private static Color GetStateColor(GlucoseUiState state) => state switch
    {
        GlucoseUiState.Normal => ColorSuccess,
        GlucoseUiState.Attention => ColorWarning,
        GlucoseUiState.Critical => ColorDanger,
        _ => ColorPrimary
    };
}
