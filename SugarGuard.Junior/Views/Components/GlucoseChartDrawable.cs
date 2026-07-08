using Microsoft.Maui.Graphics;
using SugarGuard.Shared.Constants;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Junior.Views.Components;

public class GlucoseChartDrawable : IDrawable
{
    private static readonly Color ColorSuccess = Color.FromArgb("#37A563");
    private static readonly Color ColorWarning = Color.FromArgb("#E3A32B");
    private static readonly Color ColorDanger  = Color.FromArgb("#DB5967");
    private static readonly Color ColorPrimary = Color.FromArgb("#1B8E8B");

    private readonly object _dataPointsLock = new();
    private IReadOnlyList<ChartDataPoint> _dataPoints = Array.Empty<ChartDataPoint>();

    public IReadOnlyList<ChartDataPoint> DataPoints
    {
        get
        {
            lock (_dataPointsLock)
            {
                return _dataPoints;
            }
        }
        set
        {
            lock (_dataPointsLock)
            {
                _dataPoints = value.ToArray();
            }

            InvalidateCallback?.Invoke();
        }
    }

    public Action? InvalidateCallback { get; set; }

    // Пульсация последней точки через таймер
    private WeakReference<GraphicsView>? _hostView;
    private System.Threading.Timer? _pulseTimer;
    private float _pulseRadius;
    private const float PulseMaxRadius = 14f;
    private const float PulseStep = 2f;
    private const int PulseFrameIntervalMilliseconds = 125;

    public bool ShowAxes { get; init; }

    public bool ShowValueLabels { get; init; }

    /// <summary>Привязывает GraphicsView-хост для инвалидации при анимации.</summary>
    public void AttachHost(GraphicsView view)
    {
        _hostView = new WeakReference<GraphicsView>(view);
        _pulseTimer?.Dispose();
        if (Microsoft.Maui.Storage.Preferences.Get("ui_reduce_motion", false))
        {
            _pulseTimer = null;
            _pulseRadius = 0;
            view.Invalidate();
            return;
        }

        _pulseTimer = new System.Threading.Timer(_ =>
        {
            _pulseRadius = (_pulseRadius + PulseStep) % PulseMaxRadius;
            if (_hostView.TryGetTarget(out var v))
                MainThread.BeginInvokeOnMainThread(v.Invalidate);
        }, null, 0, PulseFrameIntervalMilliseconds);
    }

    /// <summary>Останавливает анимацию (вызывать при уходе со страницы).</summary>
    public void StopAnimation()
    {
        _pulseTimer?.Dispose();
        _pulseTimer = null;
    }

    public void UpdatePulse(double value)
    {
        _pulseRadius = (float)value * PulseMaxRadius;
        Invalidate();
    }

    public void Invalidate()
    {
        InvalidateCallback?.Invoke();
    }

    public void Draw(ICanvas canvas, RectF dirtyRect)
    {
        IReadOnlyList<ChartDataPoint> points;
        lock (_dataPointsLock)
        {
            points = _dataPoints.ToArray();
        }

        if (points.Count < 2) return;

        var colorPrimary = ResolveResourceColor("ChartLineColor", ColorPrimary);
        float chartLeft   = dirtyRect.Left   + (ShowAxes ? 38f : 24f);
        float chartRight  = dirtyRect.Right  - 12f;
        float chartTop    = dirtyRect.Top    + (ShowValueLabels ? 30f : 24f);
        float chartBottom = dirtyRect.Bottom - (ShowAxes ? 28f : 24f);
        float chartWidth  = chartRight  - chartLeft;
        float chartHeight = chartBottom - chartTop;

        double yMin = Math.Min((double)points.Min(p => p.GlucoseValue), GlucoseLevels.TargetRangeMin - 1.0);
        double yMax = Math.Max((double)points.Max(p => p.GlucoseValue), GlucoseLevels.TargetRangeMax + 1.0);
        double yRange = yMax - yMin;

        long xMinTicks = points.Min(p => p.Timestamp.Ticks);
        long xMaxTicks = points.Max(p => p.Timestamp.Ticks);
        long xRange    = xMaxTicks - xMinTicks;
        if (xRange == 0) xRange = 1;

        float ToX(DateTime t) => chartLeft + (float)((t.Ticks - xMinTicks) / (double)xRange) * chartWidth;
        float ToY(decimal v)  => chartBottom - (float)(((double)v - yMin) / yRange) * chartHeight;

        float yLow  = ToY((decimal)GlucoseLevels.LowThreshold);
        float yTargetMin = ToY((decimal)GlucoseLevels.TargetRangeMin);
        float yTargetMax = ToY((decimal)GlucoseLevels.TargetRangeMax);
        float yHigh = ToY((decimal)GlucoseLevels.HighThreshold);

        if (ShowAxes)
        {
            DrawAxes(canvas, dirtyRect, chartLeft, chartRight, chartTop, chartBottom, yMin, yMax, points, ToY);
        }

        // ─── Цветные зоны диапазона ───
        canvas.SaveState();

        // Гипо (ниже LowThreshold)
        canvas.FillColor = ColorDanger.WithAlpha(0.08f);
        canvas.FillRectangle(chartLeft, yLow, chartWidth, chartBottom - yLow);

        // Цель (TargetRangeMin ~ TargetRangeMax)
        canvas.FillColor = ColorSuccess.WithAlpha(0.06f);
        canvas.FillRectangle(chartLeft, yTargetMax, chartWidth, yTargetMin - yTargetMax);

        // Гипер (выше HighThreshold)
        canvas.FillColor = ColorWarning.WithAlpha(0.08f);
        canvas.FillRectangle(chartLeft, chartTop, chartWidth, yHigh - chartTop);

        canvas.RestoreState();

        // ─── Dashed-линии ───
        canvas.SaveState();
        canvas.StrokeColor = ColorSuccess.WithAlpha(0.5f);
        canvas.StrokeSize  = 1f;
        canvas.StrokeDashPattern = new float[] { 6f, 4f };
        canvas.DrawLine(chartLeft, yTargetMin, chartRight, yTargetMin);

        canvas.StrokeColor = ColorWarning.WithAlpha(0.5f);
        canvas.DrawLine(chartLeft, yTargetMax, chartRight, yTargetMax);

        // Low и High thresholds
        canvas.StrokeColor = ColorDanger.WithAlpha(0.3f);
        canvas.StrokeDashPattern = new float[] { 3f, 3f };
        canvas.DrawLine(chartLeft, yLow, chartRight, yLow);
        canvas.DrawLine(chartLeft, yHigh, chartRight, yHigh);
        canvas.RestoreState();

        // ─── Area-fill под линией (градиент) ───
        canvas.SaveState();
        var areaPath = new PathF();
        areaPath.MoveTo(ToX(points[0].Timestamp), ToY(points[0].GlucoseValue));
        for (int i = 1; i < points.Count; i++)
            areaPath.LineTo(ToX(points[i].Timestamp), ToY(points[i].GlucoseValue));
        areaPath.LineTo(ToX(points[^1].Timestamp), chartBottom);
        areaPath.LineTo(ToX(points[0].Timestamp), chartBottom);
        areaPath.Close();

        var gradientRect = new RectF(chartLeft, chartTop, chartWidth, chartHeight);
        var gradient = new LinearGradientPaint
        {
            StartPoint = new PointF(0, 0),
            EndPoint = new PointF(0, 1),
            GradientStops = new PaintGradientStop[]
            {
                new(0f, colorPrimary.WithAlpha(0.25f)),
                new(0.5f, colorPrimary.WithAlpha(0.05f)),
                new(1f, colorPrimary.WithAlpha(0f)),
            }
        };
        canvas.SetFillPaint(gradient, gradientRect);
        canvas.FillPath(areaPath);
        canvas.RestoreState();

        // ─── Полилиния ───
        canvas.SaveState();
        canvas.StrokeColor = colorPrimary;
        canvas.StrokeSize  = 2.5f;
        canvas.StrokeDashPattern = null;
        var path = new PathF();
        path.MoveTo(ToX(points[0].Timestamp), ToY(points[0].GlucoseValue));
        for (int i = 1; i < points.Count; i++)
            path.LineTo(ToX(points[i].Timestamp), ToY(points[i].GlucoseValue));
        canvas.DrawPath(path);
        canvas.RestoreState();

        // ─── Точки данных ───
        for (int idx = 0; idx < points.Count; idx++)
        {
            var pt = points[idx];
            float cx = ToX(pt.Timestamp);
            float cy = ToY(pt.GlucoseValue);

            var pointColor = pt.UiState switch
            {
                GlucoseUiState.Normal    => ColorSuccess,
                GlucoseUiState.Attention => ColorWarning,
                GlucoseUiState.Critical  => ColorDanger,
                _                        => colorPrimary
            };

            // Последняя точка — крупнее, с ореолом
            if (idx == points.Count - 1)
            {
                canvas.SaveState();
                canvas.FillColor = pointColor.WithAlpha(0.2f);
                canvas.FillCircle(cx, cy, 12f);
                canvas.FillColor = pointColor.WithAlpha(0.4f);
                canvas.FillCircle(cx, cy, 9f);
                canvas.RestoreState();

                canvas.FillColor = Colors.White;
                canvas.FillCircle(cx, cy, 7f);
                canvas.FillColor = pointColor;
                canvas.FillCircle(cx, cy, 5f);

                // Пульсирующий ореол на последней точке
                float alpha = 1f - (_pulseRadius / PulseMaxRadius);
                canvas.StrokeColor = pointColor.WithAlpha(alpha);
                canvas.StrokeSize = 1.5f;
                canvas.DrawCircle(cx, cy, 6f + _pulseRadius);
            }
            else
            {
                canvas.FillColor = pointColor;
                canvas.FillCircle(cx, cy, 4f);
                canvas.StrokeColor = Colors.White;
                canvas.StrokeSize = 1.5f;
                canvas.DrawCircle(cx, cy, 4f);
            }

            if (ShowValueLabels && ShouldLabelPoint(idx, points.Count))
            {
                canvas.FontColor = pointColor;
                canvas.FontSize = 10f;
                canvas.Font = null;
                canvas.DrawString(
                    pt.GlucoseValue.ToString("0.0", System.Globalization.CultureInfo.InvariantCulture),
                    cx - 22f,
                    Math.Max(dirtyRect.Top, cy - 23f),
                    44f,
                    16f,
                    HorizontalAlignment.Center,
                    VerticalAlignment.Center);
            }
        }
    }

    private static bool ShouldLabelPoint(int index, int count)
    {
        if (count <= 8)
        {
            return true;
        }

        var interval = Math.Max(1, (int)Math.Ceiling((count - 1) / 6d));
        return index == 0 || index == count - 1 || index % interval == 0;
    }

    private static void DrawAxes(
        ICanvas canvas,
        RectF dirtyRect,
        float chartLeft,
        float chartRight,
        float chartTop,
        float chartBottom,
        double yMin,
        double yMax,
        IReadOnlyList<ChartDataPoint> points,
        Func<decimal, float> toY)
    {
        var axisColor = ResolveResourceColor("TextSecondary", Color.FromArgb("#667694"));
        var gridColor = ResolveResourceColor("DividerColor", Color.FromArgb("#D8E1EE"));
        var ticks = new[]
            {
                Math.Floor(yMin),
                GlucoseLevels.TargetRangeMin,
                GlucoseLevels.TargetRangeMax,
                Math.Ceiling(yMax)
            }
            .Distinct()
            .OrderBy(value => value)
            .ToArray();

        canvas.SaveState();
        canvas.FontColor = axisColor;
        canvas.FontSize = 9f;
        canvas.StrokeColor = gridColor.WithAlpha(0.65f);
        canvas.StrokeSize = 0.8f;

        foreach (var tick in ticks)
        {
            var y = toY((decimal)tick);
            if (y < chartTop - 1f || y > chartBottom + 1f)
            {
                continue;
            }

            canvas.DrawLine(chartLeft, y, chartRight, y);
            canvas.DrawString(
                tick.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture),
                dirtyRect.Left,
                y - 7f,
                chartLeft - dirtyRect.Left - 5f,
                14f,
                HorizontalAlignment.Right,
                VerticalAlignment.Center);
        }

        var first = points[0].Timestamp.ToLocalTime();
        var last = points[^1].Timestamp.ToLocalTime();
        var xLabelFormat = first.Date == last.Date ? "HH:mm" : "dd.MM";
        canvas.DrawString(first.ToString(xLabelFormat), chartLeft, chartBottom + 5f, 62f, 16f,
            HorizontalAlignment.Left, VerticalAlignment.Top);
        canvas.DrawString(last.ToString(xLabelFormat), chartRight - 62f, chartBottom + 5f, 62f, 16f,
            HorizontalAlignment.Right, VerticalAlignment.Top);
        canvas.RestoreState();
    }

    private static Color ResolveResourceColor(string key, Color fallback)
    {
        return Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : fallback;
    }
}
