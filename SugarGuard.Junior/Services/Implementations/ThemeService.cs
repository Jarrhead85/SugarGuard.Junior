// Реализация сервиса управления темой и масштабом интерфейса
namespace SugarGuard.Junior.Services.Implementations;

using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис управления темой и масштабом интерфейса.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private ScalePreset _currentScale = ScalePreset.Default;

    /// <summary>
    /// Ключи шрифтовых токенов, подлежащих масштабированию.
    /// </summary>
    private static readonly string[] FontSizeKeys =
    [
        "TextXs", "TextSm", "TextBase", "TextLg", "TextXl",
        "TextValueMd", "TextValueLg", "TextValueXl",
    ];

    /// <summary>
    /// Исходные (базовые) значения шрифтовых токенов.
    /// </summary>
    private static readonly Dictionary<string, double> DefaultFontSizes = new()
    {
        ["TextXs"] = 12,
        ["TextSm"] = 14,
        ["TextBase"] = 16,
        ["TextLg"] = 20,
        ["TextXl"] = 28,
        ["TextValueMd"] = 36,
        ["TextValueLg"] = 48,
        ["TextValueXl"] = 64,
    };

    /// <summary>
    /// Ключи отступов, подлежащих масштабированию.
    /// </summary>
    private static readonly string[] SpacingKeys =
    [
        "Spacing16", "Spacing20", "Spacing24",
    ];

    /// <summary>
    /// Исходные значения отступов.
    /// </summary>
    private static readonly Dictionary<string, double> DefaultSpacing = new()
    {
        ["Spacing16"] = 16,
        ["Spacing20"] = 20,
        ["Spacing24"] = 24,
    };

    /// <summary>
    /// Исходное значение PaddingPage.
    /// </summary>
    private const double DefaultPaddingPage = 20;

    /// <summary>
    /// Legacy-ключи для обратной совместимости.
    /// </summary>
    private static readonly string[] LegacyFontSizeKeys =
    [
        "FontSizeCaption", "FontSizeSmall", "FontSizeBody",
        "FontSizeSubheading", "FontSizeHeading", "FontSizeValue",
    ];

    private static readonly Dictionary<string, double> DefaultLegacySizes = new()
    {
        ["FontSizeCaption"] = 12,
        ["FontSizeSmall"] = 14,
        ["FontSizeBody"] = 16,
        ["FontSizeSubheading"] = 20,
        ["FontSizeHeading"] = 28,
        ["FontSizeValue"] = 48,
    };

    public ThemeService(ILogger<ThemeService> logger)
    {
        _logger = logger;
    }

    public ScalePreset CurrentScale => _currentScale;

    /// <inheritdoc />
    public double GetScaleFactor()
    {
        return _currentScale switch
        {
            ScalePreset.Small => 0.85,
            ScalePreset.Large => 1.3,
            _ => 1.0,
        };
    }

    /// <inheritdoc />
    public void ApplyScale(ScalePreset preset)
    {
        _currentScale = preset;
        var factor = GetScaleFactor();

        _logger.LogInformation("Applying scale preset {Preset} (factor={Factor})", preset, factor);

        var resources = Application.Current?.Resources;
        if (resources == null)
        {
            _logger.LogWarning("Application.Resources is null, cannot apply scale");
            return;
        }

        // Масштабируем шрифтовые токены
        foreach (var key in FontSizeKeys)
        {
            if (DefaultFontSizes.TryGetValue(key, out var baseValue))
            {
                resources[key] = baseValue * factor;
            }
        }

        // Legacy шрифтовые токены
        foreach (var key in LegacyFontSizeKeys)
        {
            if (DefaultLegacySizes.TryGetValue(key, out var baseValue))
            {
                resources[key] = baseValue * factor;
            }
        }

        // Масштабируем отступы
        foreach (var key in SpacingKeys)
        {
            if (DefaultSpacing.TryGetValue(key, out var baseValue))
            {
                resources[key] = baseValue * factor;
            }
        }

        // Масштабируем PaddingPage (Thickness)
        var scaledPadding = DefaultPaddingPage * factor;
        resources["PaddingPage"] = new Thickness(scaledPadding);

        _logger.LogInformation("Scale applied successfully");
    }
}
