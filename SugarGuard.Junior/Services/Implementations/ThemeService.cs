// Реализация сервиса управления темой и масштабом интерфейса
namespace SugarGuard.Junior.Services.Implementations;

using Microsoft.Extensions.Logging;
using Microsoft.Maui.Graphics;
using SugarGuard.Junior.Models.Enums;
using SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис управления темой и масштабом интерфейса.
/// </summary>
public class ThemeService : IThemeService
{
    private readonly ILogger<ThemeService> _logger;
    private ScalePreset _currentScale = ScalePreset.Default;
    private InterfaceSkin _currentSkin = InterfaceSkin.Neutral;

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

    public InterfaceSkin CurrentSkin => _currentSkin;

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

    public void ApplySkin(InterfaceSkin skin)
    {
        _currentSkin = skin;

        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            _logger.LogWarning("Application.Resources is null, cannot apply interface skin");
            return;
        }

        var palette = GetSkinPalette(skin);
        foreach (var (key, value) in palette)
        {
            resources[key] = Color.FromArgb(value);
        }

        var primary = Color.FromArgb(palette["BrandPrimary"]);
        var secondary = Color.FromArgb(palette["BrandSecondary"]);
        var blue = Color.FromArgb(palette["BrandBlue"]);

        // Keep all semantic aliases in sync. A number of legacy pages still
        // consume these keys, while the redesigned pages use Brand* tokens.
        resources["Primary"] = primary;
        resources["PrimaryStrong"] = secondary;
        resources["InteractivePrimary"] = primary;
        resources["InteractivePrimaryHover"] = Color.FromArgb(palette["InteractivePrimaryHover"]);
        resources["InputBorderFocused"] = primary;
        resources["LinkColor"] = blue;
        resources["ChartLineColor"] = primary;
        resources["TabBarSelectedColor"] = primary;
        resources["TabBarIndicatorColor"] = primary;
        resources["SurfacePrimarySoft"] = Color.FromArgb(skin switch
        {
            InterfaceSkin.Girl => "#20B25AC7",
            InterfaceSkin.Boy => "#1F168F9B",
            _ => "#1F1B8E8B"
        });
        resources["JuniorMascot"] = new FileImageSource
        {
            File = skin switch
            {
                InterfaceSkin.Boy => "junior_mascot_boy.png",
                InterfaceSkin.Girl => "junior_mascot_girl.png",
                _ => "junior_mascot.png"
            }
        };

        UpdateGradientBrush(
            resources,
            "PrimaryButtonBackgroundBrush",
            primary,
            blue);
        UpdateGradientBrush(
            resources,
            "PrimaryButtonPressedBackgroundBrush",
            Color.FromArgb(palette["PressedPrimary"]),
            Color.FromArgb(palette["PressedBlue"]));

        _logger.LogInformation("Interface skin applied. Skin={Skin}", skin);
    }

    private static IReadOnlyDictionary<string, string> GetSkinPalette(InterfaceSkin skin)
    {
        return skin switch
        {
            InterfaceSkin.Boy => new Dictionary<string, string>
            {
                ["BrandPrimary"] = "#168F9B",
                ["BrandSecondary"] = "#4ED7C2",
                ["BrandBlue"] = "#277FE5",
                ["PressedPrimary"] = "#117681",
                ["PressedBlue"] = "#1E67BD",
                ["InteractivePrimary"] = "#168F9B",
                ["InteractivePrimaryHover"] = "#127682",
                ["SurfaceSelected"] = "#1F168F9B",
                ["BrandAccentBadgeBg"] = "#1F168F9B"
            },
            InterfaceSkin.Girl => new Dictionary<string, string>
            {
                ["BrandPrimary"] = "#B25AC7",
                ["BrandSecondary"] = "#F09ACB",
                ["BrandBlue"] = "#8B7CF6",
                ["PressedPrimary"] = "#9047A2",
                ["PressedBlue"] = "#6E62C7",
                ["InteractivePrimary"] = "#B25AC7",
                ["InteractivePrimaryHover"] = "#9347A6",
                ["SurfaceSelected"] = "#20B25AC7",
                ["BrandAccentBadgeBg"] = "#20B25AC7"
            },
            _ => new Dictionary<string, string>
            {
                ["BrandPrimary"] = "#1B8E8B",
                ["BrandSecondary"] = "#56D0BF",
                ["BrandBlue"] = "#2678D9",
                ["PressedPrimary"] = "#167B79",
                ["PressedBlue"] = "#1F69C1",
                ["InteractivePrimary"] = "#1B8E8B",
                ["InteractivePrimaryHover"] = "#177D79",
                ["SurfaceSelected"] = "#1F1B8E8B",
                ["BrandAccentBadgeBg"] = "#1F1B8E8B"
            }
        };
    }

    private static void UpdateGradientBrush(
        ResourceDictionary resources,
        string key,
        Color start,
        Color end)
    {
        if (resources.TryGetValue(key, out var value)
            && value is LinearGradientBrush brush
            && brush.GradientStops.Count >= 2)
        {
            brush.GradientStops[0].Color = start;
            brush.GradientStops[1].Color = end;
            return;
        }

        resources[key] = new LinearGradientBrush(
            new GradientStopCollection
            {
                new GradientStop(start, 0f),
                new GradientStop(end, 1f)
            },
            new Point(0, 0),
            new Point(1, 0));
    }
}
