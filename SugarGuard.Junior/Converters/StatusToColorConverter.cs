// SugarGuard.Junior/Converters/StatusToColorConverter.cs
//
// Конвертер: строковый ключ цвета (семантический токен) → Color из ресурсов темы.
//
// Используется в XAML вместо хардкодных hex-значений.
// Принимает строку-ключ (например "Danger", "Warning", "Success", "Primary")
// и возвращает соответствующий Color из ResourceDictionary приложения.
//
// Это позволяет теме (светлой / тёмной) автоматически подставлять
// правильный цвет — без изменения ViewModel или XAML-разметки.
//
// Связанные файлы:
//   — Resources/Styles/Colors.xaml      : определение всех semantic keys
//   — Resources/Styles/LightTheme.xaml  : значения цветов для светлой темы
//   — Resources/Styles/DarkTheme.xaml   : значения цветов для тёмной темы
//   — ViewModels/RecommendationModalViewModel.cs : использует UrgencyColorKey
//
// Пример использования в XAML:
//   <Label TextColor="{Binding UrgencyColorKey,
//                      Converter={StaticResource StatusToColorConverter}}" />
//
// Регистрация в App.xaml или в ResourceDictionary страницы:
//   <converters:StatusToColorConverter x:Key="StatusToColorConverter" />

using System.Globalization;

namespace SugarGuard.Junior.Converters;

/// <summary>
/// Конвертирует строковый семантический ключ цвета в объект <see cref="Color"/>
/// из глобального ResourceDictionary приложения.
///
/// Поддерживаемые ключи (определены в Colors.xaml):
///   "Danger"      — критическое состояние (низкий / высокий сахар)
///   "DangerSoft"  — мягкий фон для danger-состояния
///   "Warning"     — предупреждение (сахар вне целевого диапазона)
///   "WarningSoft" — мягкий фон для warning-состояния
///   "Success"     — норма (сахар в целевом диапазоне)
///   "SuccessSoft" — мягкий фон для success-состояния
///   "Primary"     — основной акцентный цвет бирюза
///   "TextMuted"   — приглушённый текст (нейтральное состояние)
///
/// Если ключ не найден в ресурсах — используется безопасный fallback-цвет.
/// </summary>
public class StatusToColorConverter : IValueConverter
{
    // ── Fallback-цвета на случай отсутствия ключа в ResourceDictionary ──────
    // Совпадают с дефолтными значениями из UI Kit (п. 2 дизайн-системы).
    private static readonly Color FallbackDanger = Color.FromArgb("#DB5967");
    private static readonly Color FallbackWarning = Color.FromArgb("#E3A32B");
    private static readonly Color FallbackSuccess = Color.FromArgb("#37A563");
    private static readonly Color FallbackPrimary = Color.FromArgb("#1B8E8B");
    private static readonly Color FallbackMuted = Color.FromArgb("#96A2B8");

    /// <inheritdoc />
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // Получаем строковый ключ из binding-значения
        var key = value as string;

        // Если значение пустое — возвращаем нейтральный цвет
        if (string.IsNullOrWhiteSpace(key))
            return FallbackMuted;

        // Пытаемся найти цвет в ResourceDictionary текущей темы
        if (TryGetResourceColor(key, out var color))
            return color;

        // Ключ не найден в ресурсах — используем встроенный fallback.
        // Это защищает UI от краша, если Colors.xaml ещё не полностью настроен.
        return key switch
        {
            "Danger" or "DangerSoft" => FallbackDanger,
            "Warning" or "WarningSoft" => FallbackWarning,
            "Success" or "SuccessSoft" => FallbackSuccess,
            "Primary" => FallbackPrimary,
            _ => FallbackMuted,
        };
    }

    /// <inheritdoc />
    /// <remarks>
    /// Обратное преобразование не поддерживается —
    /// Color → строковый ключ не нужен в SugarGuard.
    /// </remarks>
    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException(
            $"{nameof(StatusToColorConverter)} не поддерживает обратное преобразование.");

    // ── Вспомогательный метод ────────────────────────────────────────────────

    /// <summary>
    /// Пытается получить <see cref="Color"/> из глобального ResourceDictionary
    /// по указанному ключу.
    /// Безопасен при <c>Application.Current == null</c> (unit-тесты).
    /// </summary>
    /// <param name="key">Семантический ключ цвета из Colors.xaml.</param>
    /// <param name="color">Найденный цвет или <c>null</c>.</param>
    /// <returns><c>true</c> если цвет найден и является <see cref="Color"/>.</returns>
    private static bool TryGetResourceColor(string key, out Color? color)
    {
        color = null;

        // Защита от null при запуске unit-тестов без MAUI-хоста
        var resources = Application.Current?.Resources;
        if (resources is null)
            return false;

        if (resources.TryGetValue(key, out var raw) && raw is Color found)
        {
            color = found;
            return true;
        }

        return false;
    }
}
