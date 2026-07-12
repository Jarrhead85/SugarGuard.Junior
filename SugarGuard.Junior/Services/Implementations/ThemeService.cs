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

    public void ApplySkin(InterfaceSkin skin, bool isDarkTheme)
    {
        _currentSkin = skin;

        var resources = Application.Current?.Resources;
        if (resources is null)
        {
            _logger.LogWarning("Application.Resources is null, cannot apply interface skin");
            return;
        }

        var palette = GetSkinPalette(skin, isDarkTheme);
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
            InterfaceSkin.Watercolor => "#26459CB6",
            _ => "#1F1B8E8B"
        });
        resources["BackgroundPage"] = Color.FromArgb(palette["SkinBackgroundPage"]);
        resources["BackgroundSubtle"] = Color.FromArgb(palette["SkinBackgroundSubtle"]);
        resources["SurfaceOffset"] = Color.FromArgb(palette["SkinSurfaceOffset"]);
        resources["TabBarActivePillBackground"] = Color.FromArgb(palette["SurfaceSelected"]);

        // Theme dictionaries can be reloaded after page creation on Android.
        // Re-apply every semantic alias here so existing DynamicResource bindings
        // never end up with a dark page and light cards (or the opposite).
        var surface = Color.FromArgb(isDarkTheme ? "#111825" : "#FFFFFF");
        var surfaceCard = Color.FromArgb(isDarkTheme ? "#E6111825" : "#F2FFFFFF");
        var surfaceSubtle = Color.FromArgb(isDarkTheme ? "#1A2333" : "#F4F7FB");
        var textPrimary = Color.FromArgb(isDarkTheme ? "#EDF4FF" : "#16213E");
        var textSecondary = Color.FromArgb(isDarkTheme ? "#9EAED0" : "#667694");
        var textFaint = Color.FromArgb(isDarkTheme ? "#6F7D99" : "#96A2B8");
        var border = Color.FromArgb(isDarkTheme ? "#304255" : "#D8E1EE");
        var divider = Color.FromArgb(isDarkTheme ? "#17EDF4FF" : "#1416213E");

        resources["Surface"] = surface;
        resources["Surface2"] = surfaceSubtle;
        resources["SurfaceCard"] = surfaceCard;
        resources["SurfaceCardStrong"] = surface;
        resources["SurfaceElevated"] = surfaceCard;
        resources["SurfaceSolid"] = surface;
        resources["SurfaceSubtle"] = surfaceSubtle;
        resources["SurfaceInput"] = surfaceCard;
        resources["TextPrimary"] = textPrimary;
        resources["TextSecondary"] = textSecondary;
        resources["TextMuted"] = textSecondary;
        resources["TextFaint"] = textFaint;
        resources["TextFaintColor"] = textFaint;
        resources["TextPlaceholder"] = textFaint;
        resources["Border"] = border;
        resources["BorderDefault"] = border;
        resources["SurfaceCardStroke"] = border;
        resources["InputBorderColor"] = border;
        resources["Divider"] = divider;
        resources["DividerColor"] = divider;
        resources["TabBarBackground"] = surfaceCard;
        resources["TabBarBackgroundColor"] = surfaceCard;
        resources["TabBarBorder"] = border;
        resources["TabBarInactiveColor"] = textFaint;
        resources["TabBarUnselectedColor"] = textFaint;
        resources["JuniorMascot"] = new FileImageSource
        {
            File = skin switch
            {
                InterfaceSkin.Boy => "junior_mascot_boy_v2.png",
                InterfaceSkin.Girl => "junior_mascot_girl_v2.png",
                InterfaceSkin.Watercolor => "junior_mascot_watercolor.png",
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

        _logger.LogInformation(
            "Interface skin applied. Skin={Skin} Theme={Theme}",
            skin,
            isDarkTheme ? "Dark" : "Light");
    }

    private static IReadOnlyDictionary<string, string> GetSkinPalette(
        InterfaceSkin skin,
        bool isDarkTheme)
    {
        if (isDarkTheme)
        {
            return skin switch
            {
                InterfaceSkin.Boy => new Dictionary<string, string>
                {
                    ["BrandPrimary"] = "#55D8C4",
                    ["BrandSecondary"] = "#79E6D5",
                    ["BrandBlue"] = "#72ADFF",
                    ["PressedPrimary"] = "#39B8A7",
                    ["PressedBlue"] = "#548EDB",
                    ["InteractivePrimary"] = "#55D8C4",
                    ["InteractivePrimaryHover"] = "#79E6D5",
                    ["SurfaceSelected"] = "#3355D8C4",
                    ["BrandAccentBadgeBg"] = "#3355D8C4",
                    ["SkinBackgroundPage"] = "#081424",
                    ["SkinBackgroundSubtle"] = "#0D2036",
                    ["SkinSurfaceOffset"] = "#132C44"
                },
                InterfaceSkin.Girl => new Dictionary<string, string>
                {
                    ["BrandPrimary"] = "#D58BE4",
                    ["BrandSecondary"] = "#F4B0DA",
                    ["BrandBlue"] = "#B2A7FF",
                    ["PressedPrimary"] = "#B86FC9",
                    ["PressedBlue"] = "#9185E1",
                    ["InteractivePrimary"] = "#D58BE4",
                    ["InteractivePrimaryHover"] = "#E4A3EF",
                    ["SurfaceSelected"] = "#33D58BE4",
                    ["BrandAccentBadgeBg"] = "#33D58BE4",
                    ["SkinBackgroundPage"] = "#160D1E",
                    ["SkinBackgroundSubtle"] = "#221329",
                    ["SkinSurfaceOffset"] = "#2A1733"
                },
                InterfaceSkin.Watercolor => new Dictionary<string, string>
                {
                    ["BrandPrimary"] = "#61CFC6",
                    ["BrandSecondary"] = "#9BE6D5",
                    ["BrandBlue"] = "#74A8F4",
                    ["PressedPrimary"] = "#3EAFA6",
                    ["PressedBlue"] = "#4F82CC",
                    ["InteractivePrimary"] = "#61CFC6",
                    ["InteractivePrimaryHover"] = "#8AE1D3",
                    ["SurfaceSelected"] = "#3361CFC6",
                    ["BrandAccentBadgeBg"] = "#3361CFC6",
                    ["SkinBackgroundPage"] = "#101A25",
                    ["SkinBackgroundSubtle"] = "#172939",
                    ["SkinSurfaceOffset"] = "#1C3346"
                },
                _ => new Dictionary<string, string>
                {
                    ["BrandPrimary"] = "#56D0BF",
                    ["BrandSecondary"] = "#7BE1D2",
                    ["BrandBlue"] = "#6DAEFF",
                    ["PressedPrimary"] = "#3EB6A6",
                    ["PressedBlue"] = "#528FD9",
                    ["InteractivePrimary"] = "#56D0BF",
                    ["InteractivePrimaryHover"] = "#76E0D0",
                    ["SurfaceSelected"] = "#3356D0BF",
                    ["BrandAccentBadgeBg"] = "#3356D0BF",
                    ["SkinBackgroundPage"] = "#0B1018",
                    ["SkinBackgroundSubtle"] = "#0F1520",
                    ["SkinSurfaceOffset"] = "#10192A"
                }
            };
        }

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
                ["BrandAccentBadgeBg"] = "#1F168F9B",
                ["SkinBackgroundPage"] = "#EEF8FF",
                ["SkinBackgroundSubtle"] = "#DCEFFF",
                ["SkinSurfaceOffset"] = "#E3F4FF"
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
                ["BrandAccentBadgeBg"] = "#20B25AC7",
                ["SkinBackgroundPage"] = "#FFF3FA",
                ["SkinBackgroundSubtle"] = "#FFE4F3",
                ["SkinSurfaceOffset"] = "#FFF0F8"
            },
            InterfaceSkin.Watercolor => new Dictionary<string, string>
            {
                ["BrandPrimary"] = "#279C98",
                ["BrandSecondary"] = "#6DD8C9",
                ["BrandBlue"] = "#4E8FE8",
                ["PressedPrimary"] = "#197F7C",
                ["PressedBlue"] = "#3674BF",
                ["InteractivePrimary"] = "#279C98",
                ["InteractivePrimaryHover"] = "#187D7A",
                ["SurfaceSelected"] = "#21279C98",
                ["BrandAccentBadgeBg"] = "#21279C98",
                ["SkinBackgroundPage"] = "#F7F4EC",
                ["SkinBackgroundSubtle"] = "#E7F2EF",
                ["SkinSurfaceOffset"] = "#EDF7F4"
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
                ["BrandAccentBadgeBg"] = "#1F1B8E8B",
                ["SkinBackgroundPage"] = "#F4F7FB",
                ["SkinBackgroundSubtle"] = "#E9EFF9",
                ["SkinSurfaceOffset"] = "#E9EFF9"
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
