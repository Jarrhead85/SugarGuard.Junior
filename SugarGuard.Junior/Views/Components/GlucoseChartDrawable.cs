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

    private IReadOnlyList<ChartDataPoint> _dataPoints = Array.Empty<ChartDataPoint>();

    public IReadOnlyList<ChartDataPoint> DataPoints
    {
        get => _dataPoints;
        set
        {
            _dataPoints = value;
            InvalidateCallback?.Invoke();
        }
    }

    public Action? InvalidateCallback { get; set; }

    // Пульсация последней точки через таймер
    private WeakReference<GraphicsView>? _hostView;
    private System.Threading.Timer? _pulseTimer;
    private float _pulseRadius;
    private const float PulseMaxRadius = 14f;
    private const float PulseStep = 0.8f;

    /// <summary>Привязывает GraphicsView-хост для инвалидации при анимации.</summary>
    public void AttachHost(GraphicsView view)
    {
        _hostView = new WeakReference<GraphicsView>(view);
        _pulseTimer?.Dispose();
        _pulseTimer = new System.Threading.Timer(_ =>
        {
            _pulseRadius = (_pulseRadius + PulseStep) % PulseMaxRadius;
            if (_hostView.TryGetTarget(out var v))
                MainThread.BeginInvokeOnMainThread(v.Invalidate);
        }, null, 0, 50);
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
        var points = _dataPoints;
        if (points.Count < 2) return;

        const float padding = 24f;
        float chartLeft   = dirtyRect.Left   + padding;
        float chartRight  = dirtyRect.Right  - padding;
        float chartTop    = dirtyRect.Top    + padding;
        float chartBottom = dirtyRect.Bottom - padding;
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
                new(0f, ColorPrimary.WithAlpha(0.25f)),
                new(0.5f, ColorPrimary.WithAlpha(0.05f)),
                new(1f, ColorPrimary.WithAlpha(0f)),
            }
        };
        canvas.SetFillPaint(gradient, gradientRect);
        canvas.FillPath(areaPath);
        canvas.RestoreState();

        // ─── Полилиния ───
        canvas.SaveState();
        canvas.StrokeColor = ColorPrimary;
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
                _                        => ColorPrimary
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
        }
    }
}
