# SugarGuard UI Kit v2.1

> Актуально на 09.07.2026. UI Kit описывает текущий визуальный стандарт SugarGuard: детское MAUI-приложение, web-кабинеты родителя/врача/администратора, адаптивные sidebar/topbar, дневник питания, ачивки и скины интерфейса.
## §1. Принципы дизайна

SugarGuard — медицинское приложение. Дизайн обязан быть:

- **Чётким:** ключевая информация (значение глюкозы, статус) читается за < 1 секунды.
- **Спокойным:** ни одна декоративная деталь не конкурирует с данными.
- **Живым:** micro-animations дают ощущение отклика, не превращая интерфейс в шоу.
- **Доступным:** минимальный контраст текст/фон — 4.5:1 (WCAG AA). Все кнопки ≥ 52×52 dp.

### Анти-паттерны (запрещено)

- Blur-glassmorphism (тяжёлый `BackdropFilter`) — убивает FPS на Android mid-range.
  Разрешён только полупрозрачный фон (`#D6FFFFFF` / `#CC111825`) без blur.
- Цветные левые бордюры у карточек (`BorderLeft: 3px solid accent`).
- Emoji как иконки в производственном UI.
- Одинаковый `CornerRadius` у вложенных элементов (без учёта padding).
  Правило: `inner_radius = outer_radius - gap`.
- Текст мельче 12sp/12pt в любом состоянии.

---

## §1.1. Маскот, ачивки и скины MAUI

Мобильное приложение — детский помощник. Визуальный стиль может быть мягче и дружелюбнее, чем web-кабинеты, но медицинские данные всегда остаются главным слоем.

### Маскот

- Базовый персонаж: капля SugarGuard с рюкзаком/щитком.
- Маскот используется в hero-карточках, пустых состояниях, ачивках и мягких подсказках.
- Маскот не должен закрывать значения глюкозы, ХЕ, инсулина, кнопки сохранения и предупреждения.

### Скины

Скины меняют только палитру, декоративные элементы и вариант маскота:

| Скин | Палитра | Назначение |
|------|---------|------------|
| `Neutral` | teal + blue | Универсальный режим по умолчанию |
| `Boy` | blue + mint | Более холодная гамма |
| `Girl` | coral + lilac + mint | Более тёплая гамма |

Структура экранов, размеры контролов и порядок действий не меняются между скинами. Это важно для привычки ребёнка и поддержки родителем.

### Ачивки

Ачивки — не медицинская оценка ребёнка, а мягкая мотивация. Формулировки должны быть позитивными:

- `10 дней измерений подряд`
- `Неделя в целевом диапазоне`
- `Рюкзак собран`
- `Дневник питания заполнен`
- `Напоминания без пропусков`

Каждая ачивка имеет простую иллюстрацию, короткое название и одну строку объяснения.

---

## §2. Цветовая палитра

Все цвета определены в `Colors.xaml` (MAUI) и `variables.css` (Web). **Не изменять гексы без ревью.**

### Бренд

| Токен | Light | Dark | Назначение |
|-------|-------|------|-----------|
| `Primary` | `#1B8E8B` | `#56D0BF` | Акцент, CTA, активные состояния |
| `PrimaryStrong` | `#56D0BF` | `#56D0BF` | Hover/pressed Primary |
| `PrimaryAlt` | `#2678D9` | `#6DAEFF` | Вторичный акцент (синий) |

### Семантика глюкозы

| Токен | Light | Dark | Порог |
|-------|-------|------|-------|
| `GlucoseNormal` | `#37A563` | `#62D889` | 4.0–10.0 ммоль/л |
| `GlucoseWarning` | `#E3A32B` | `#F4BC56` | 3.0–3.9 или 10.1–13.9 |
| `GlucoseDanger` | `#DB5967` | `#FF7A8B` | < 3.0 или ≥ 14.0 |

Бизнес-константы пороговых значений хранятся исключительно в `SugarGuard.Shared.GlucoseLevels`.
UI никогда не хардкодит числа — только ссылается на `GlucoseLevels.*`.

### Нейтральные поверхности

| Токен | Light | Dark |
|-------|-------|------|
| `BackgroundPage` | `#F4F7FB` | `#0B1018` |
| `SurfaceCard` | `#D6FFFFFF` (85%) | `#CC111825` (80%) |
| `SurfaceElevated` | `#F2FFFFFF` (95%) | `#E6111825` (90%) |
| `SurfaceOffset` | `#E9EFF9` | `#10192A` |
| `TextPrimary` | `#16213E` | `#EDF4FF` |
| `TextSecondary` | `#667694` | `#9EAED0` |
| `TextFaint` | `#96A2B8` | `#6F7D99` |

### UiState → цвет (GlucoseUiStateService)

```
Normal    → GlucoseNormal (зелёный)
Attention → GlucoseWarning (жёлтый)
Critical  → GlucoseDanger (красный)
```

5 бизнес-уровней (`CriticallyLow / Low / Normal / High / CriticallyHigh`) → 3 UI-состояния.
`GlucoseUiStateService.Resolve()` — единственная точка маппинга.

---

## §3. Типографика

Шрифтовая модель разделена по платформам:

- **Web:** `ClashDisplay` для крупных заголовков/KPI, `Satoshi` для UI-текста.
- **MAUI:** `Montserrat` для крупных заголовков/KPI, `Nunito` для основного детского UI-текста.

Токены ниже задают роль текста. Конкретный файл шрифта берётся из платформенного маппинга (`Typography.xaml` / web CSS variables).

| Токен | Размер | Шрифт | Применение |
|-------|--------|-------|-----------|
| `TextValueXl` | 56sp | Display Bold | Героическое значение глюкозы |
| `TextValueLg` | 40sp | Display Bold | Крупный KPI |
| `TextValueMd` | 28sp | Display Bold | Обычный KPI |
| `TextXl` | 24sp | Display Bold | Заголовок страницы |
| `TextLg` | 18sp | Body Bold | Заголовок секции |
| `TextBase` | 15sp | Body Regular | Основной текст |
| `TextSm` | 13sp | Body Regular | Подписи, caption |
| `TextXs` | 12sp | Body Regular | Chips, badges, метки (абсолютный минимум) |

**Правило дисплей/текст:**
- Display-шрифт — только от `TextLg` (18sp) и выше, когда это заголовок или KPI.
- Body-шрифт — всё остальное: кнопки, подписи, тело, chips.
- В кнопках: Body Bold, 15sp.

---

## §4. Отступы и радиусы

### Spacing (4dp-система)

```
Spacing0  = 0   Spacing4  = 4   Spacing8  = 8
Spacing12 = 12  Spacing16 = 16  Spacing20 = 20
Spacing24 = 24  Spacing32 = 32  Spacing40 = 40
Spacing48 = 48  Spacing64 = 64
```

Стандартный padding страницы: `PaddingPage = 20dp`.
Padding hero-экранов: `PaddingPageHero = 32,64,32,32`.

### Border Radius

| Токен | Значение | Применение |
|-------|---------|-----------|
| `RadiusSm` | 14 | Поля ввода, небольшие вставки |
| `RadiusMd` | 18 | Кнопки, карточки списков |
| `RadiusLg` | 24 | Карточки данных, диалоги |
| `RadiusXl` | 32 | Hero-карточки, bottom sheet |
| `ChipRadius` | 999 | Чипы (pill-shape) |

**Вложенный радиус:**
Внутренний элемент в карточке с `RadiusLg=24` и padding=12: `inner = 24 - 12 = 12`.

---

## §5. Компоненты — Карточки и MetricCard

### BaseCard

Все карточки наследуют `BaseCard`:
- Фон: `SurfaceCard` (стекломорфизм без blur)
- Обводка: `1px`, `#1A16213E` (light) / `#17EDF4FF` (dark)
- `CornerRadius`: 24
- `Shadow`: `CardShadowBase`

### Типы карточек

| Стиль | Ключ | Особенность |
|-------|------|-------------|
| Базовая | `Card` | Белый/тёмный glassmorphism |
| Hero | `HeroCard` | Градиент `#F4FFFFFF → #1B8E8B → #2678D9`, glow-тень |
| Data | `DataCard` | Чуть более насыщенный фон, DataShadow |
| Action | `ActionCard` | Лёгкий градиент, ActionGlow-тень |
| List | `ListCard` | Тонкий, компактный padding=16 |

### MetricCard (KPI-блок)

```
┌─────────────────────────────┐
│  [Иконка]   Метка           │
│                             │
│  7.4          ← TextValueMd │
│  ммоль/л      ← TextSm      │
│                             │
│  ↑ 0.3  [chip: Норма]   [●] │ ← pending sync dot (NEW v2)
└─────────────────────────────┘
```

**NEW v2 — Pending Sync Indicator:**
Маленький кружок `8×8dp` цвета `SyncStatusPending` в правом нижнем углу карточки.
Появляется только если `IsSynced == false`. Tooltip/SemanticDescription: «Ожидает отправки».
Анимация: пульс (opacity 1.0 → 0.4 → 1.0, duration 1200ms, repeat).

```xaml
<!-- Pending sync dot — добавить в Grid карточки -->
<Ellipse
    WidthRequest="8" HeightRequest="8"
    Fill="{DynamicResource SyncStatusPending}"
    HorizontalOptions="End" VerticalOptions="End"
    Margin="0,0,4,4"
    IsVisible="{Binding IsPendingSync}"
    AutomationProperties.Name="Ожидает отправки на сервер" />
```

---

## §6. Компоненты — Баннеры состояний

### StateBanner

Тонкая полоска под AppBar или inline в контент.

| Состояние | Ключ | Цвет | Иконка |
|-----------|------|------|--------|
| Loading | `StateBannerLoading` | Primary | spinner |
| Success | `StateBannerSuccess` | GlucoseNormal | ✓ |
| Warning | `StateBannerWarning` | GlucoseWarning | ⚠ |
| Error | `StateBannerError` | GlucoseDanger | ✕ |
| Empty | `StateBannerEmpty` | TextFaint | ○ |
| SyncPending | `StateBannerSync` | PrimaryAlt | ↑ |
| Offline | `StateBannerOffline` | TextSecondary | wifi-off |

### SyncBanner

Полоска вверху страницы, показывается при активной синхронизации или ошибке.

| Вариант | Когда |
|---------|-------|
| `syncing` | `IsSyncing = true` — анимированный прогресс |
| `pending` | `PendingItemsCount > 0`, сеть есть — «Ожидает отправки N записей» |
| `offline` | `IsConnected = false` — «Нет соединения. Данные сохранены локально» |
| `conflict` | **NEW v2** — `PendingSyncConflicts > 0` — «N конфликтов требуют внимания» + кнопка «Разрешить» |
| `error` | Ошибка последней синхронизации — красный, кнопка «Повторить» |

```
conflict-вариант:
┌─────────────────────────────────────────┐
│ ⚡  2 конфликта требуют внимания  [→]   │
│     цвет: GlucoseWarning (жёлтый)       │
└─────────────────────────────────────────┘
```

### EmptyStateView

Три варианта размера:

| Размер | Иконка | Заголовок | Описание | CTA |
|--------|--------|-----------|----------|-----|
| Small | 24dp | нет | 1 строка | нет |
| Medium | 40dp | есть | 2 строки | опционально |
| Large | 56dp | есть | 3 строки + иллюстрация | обязательно |

Правило: **никогда не показывать просто «Нет данных»**. Всегда объяснять, что нужно сделать.

---

## §7. Кнопки

| Стиль | Ключ | Использование |
|-------|------|--------------|
| Primary | `PrimaryButton` | Главное действие на экране (одно!) |
| Secondary | `SecondaryButton` | Альтернативное действие |
| Soft | `SoftButton` | Вторичные действия без акцента |
| Ghost | `GhostButton` | Деструктивные действия или «Отмена» |
| Destructive | (цвет `GlucoseDanger`) | Удаление, выход, отвязка |

Минимальная область касания: **52×52dp**. Padding кнопки по умолчанию: `16,14`.

**VisualState:**
- `Normal` → opacity 1.0, scale 1.0
- `Pressed` → scale 0.985, opacity 0.97 (transition 120ms)
- `Disabled` → opacity 1.0, scale 1.0, цвет `#8A99A8` (light) / `#7D8CA3` (dark)

---

## §8. Графики глюкозы

### Линия + Area-fill (NEW v2 — добавлено)

`GlucoseChartDrawable` (реализует `IDrawable`) должен рисовать:

1. **Зоны диапазона** — прямоугольники за данными:
   - Hypoglycemia zone (< 3.9): `GlucoseDanger` opacity 0.06
   - Target range (4.0–10.0): `GlucoseNormal` opacity 0.08
   - Hyperglycemia zone (> 10.0): `GlucoseWarning` opacity 0.06

2. **Area-fill под линией** (NEW):
   ```csharp
   // LinearGradientPaint от цвета состояния к прозрачному
   // Высота заливки: от Y-координаты линии до нижней границы зоны данных (dirtyRect.Bottom - paddingBottom)
   // Opacity градиента: top = 0.28, bottom = 0.0
   
   var fillPath = new PathF();
   fillPath.MoveTo(points[0].X, dirtyRect.Bottom - paddingBottom);
   fillPath.LineTo(points[0].X, points[0].Y);
   for (int i = 1; i < points.Count; i++)
       fillPath.LineTo(points[i].X, points[i].Y);
   fillPath.LineTo(points[^1].X, dirtyRect.Bottom - paddingBottom);
   fillPath.Close();
   
   // canvas.SetFillPaint не поддерживает gradient напрямую в IDrawable.
   // Используем: canvas.FillColor = stateColor.WithAlpha(0.18f) для заливки PathF.
   // Для продвинутого градиента — SKCanvas через SkiaSharp.
   canvas.FillColor = stateColor.WithAlpha(0.18f);
   canvas.FillPath(fillPath);
   ```

3. **Линия** — поверх заливки, `StrokeSize = 2f`, `LineMode.Spline`.

4. **Последняя точка** — пульсирующий круг:
   - Внешний: `stateColor.WithAlpha(0.18f)`, radius 7dp
   - Внутренний: `stateColor`, radius 4dp

5. **Пустое состояние** — пунктирная горизонтальная линия посередине canvas.

6. **Целевой диапазон (dashed)** — горизонтальные линии на Y-позиции 4.0 и 10.0,
   `StrokeDashPattern = [5f, 4f]`, `StrokeColor = Primary.WithAlpha(0.3f)`.

### Мини-график (MainPage hero)

- Canvas: ширина = ширина карточки, высота 64dp.
- Только линия + area-fill, без осей и меток.
- Цвет линии по `GlucoseUiState`.

---

## §9. Чипы (StatusChip, FilterChip)

### StatusChip — глюкоза

| Состояние | Контейнер BG | Контейнер Border | Текст |
|-----------|-------------|-----------------|-------|
| Normal | `#1F37A563` | `#3337A563` | `#216E43` / `#DFFFE A` |
| Warning | `#1FE3A32B` | `#33E3A32B` | `#8B5C00` / `#FFF2D1` |
| Danger | `#1FDB5967` | `#33DB5967` | `#A43846` / `#FFE5E8` |

### FilterChip

Неактивный: фон `#F7FFFFFF` / `#B81A1A1A`, граница `#1C16213E`.
Активный: фон `#1F1B8E8B` (light) / `#2B56D0BF` (dark), граница `#331B8E8B`.

Размер: высота 32dp, padding `12,6`, radius 999 (pill).

---

## §10. Поля ввода (Entry / Editor)

```
┌─────────────────────────────┐  ← border: InputBorderColor (1.5px)
│  Метка поля           ↑ sm  │  ← TextSecondary, 12sp
│                             │
│  Значение                   │  ← TextPrimary, 15sp
│                             │
└─────────────────────────────┘
   Helper text / Ошибка       ← TextFaint / DangerText, 12sp
```

Состояния:
- Default: `InputBorderColor` (`#2616213E` / `#22EDF4FF`)
- Focus: `InputBorderFocused` = `Primary`
- Error: `InputBorderError` = `GlucoseDanger`
- Disabled: opacity 0.5

Padding внутри поля: `12,0` (горизонтально) + минимальная высота 52dp.

### CodeInput (NEW v2 — для верификации)

6 отдельных однозначных боксов в ряд:

```
  ┌───┐ ┌───┐ ┌───┐  ┌───┐ ┌───┐ ┌───┐
  │ 4 │ │ 8 │ │ 3 │  │   │ │   │ │   │
  └───┘ └───┘ └───┘  └───┘ └───┘ └───┘
         ↑ активный (Primary border)
```

- Размер бокса: 48×56dp
- Шрифт: ClashDisplay Bold, 24sp, `HorizontalTextAlignment = Center`
- Border radius: 14
- Пробел после 3-го бокса: `Spacing8`
- При заполнении автоматически переходит к следующему боксу
- Заполненный: фон `SurfacePrimarySoft`, граница `Primary`
- Пустой: фон `SurfaceCard`, граница `InputBorderColor`
- Ошибка: все боксы — граница `InputBorderError`, лёгкий shake-animation

---

## §11. Bottom Sheet и диалоги

### Bottom Sheet

- Фон: `BottomSheetBackground` (`#F2FFFFFF` / `#E6111825`)
- Радиус верхних углов: 32
- Handle: `#2616213E` / `#33EDF4FF`, 32×4dp, centered, `Margin = 0,12,0,8`
- Backdrop scrim: `#4016213E` / `#66000000`
- Анимация: slide-up из-за нижнего края, duration 280ms, easing `CubicOut`

### Диалоги

- Radius: 24
- Padding: 24
- Overlay: scrim как у Bottom Sheet
- Анимация: fade-in + scale 0.92→1.0, duration 200ms

### Confirmation Dialog

Обязателен для деструктивных действий (удаление, разрыв связки, выход).

```
┌───────────────────────────────┐
│  Заголовок (Satoshi Bold 18)  │
│                               │
│  Текст подтверждения (15sp)   │
│                               │
│  [Ghost: Отмена]  [Primary/   │
│                   Destructive]│
└───────────────────────────────┘
```

---

## §12. Навигация

### Tab Bar (мобильное приложение)

- Фон: `TabBarBackground` (`#D6FFFFFF` / `#CC111825`)
- Граница: `TabBarBorder` (`#1A16213E` / `#17EDF4FF`)
- Активная вкладка: иконка + label цветом `TabBarActive` = `Primary`
- Активная таблетка: `TabBarActivePillBackground` `32,8,32,8` padding, radius 999
- Неактивная: `TabBarInactive` = `TextFaint`
- Высота: 56dp + safe area inset

Количество вкладок: 3–5. При > 5 — убирать метки и/или перегруппировать.

### AppBar

- Фон прозрачный или `SurfaceCard`
- Заголовок: ClashDisplay Bold / Satoshi SemiBold, 17sp, `TextPrimary`
- Кнопки навигации: 44×44dp minimum tap target
- Leading: «Назад» с иконкой ← , trailing: иконки действий

---

## §13. Child Mode — масштаб интерфейса (NEW v2)

Child Mode позволяет увеличить весь интерфейс для удобства ребёнка или людей с нарушениями зрения.

### Три пресета

| Пресет | Ключ | Масштаб шрифта | Масштаб иконок | Spacing |
|--------|------|---------------|---------------|---------|
| Маленький | `scale_small` | × 0.85 | × 0.85 | × 0.90 |
| Стандарт | `scale_default` | × 1.00 | × 1.00 | × 1.00 |
| Крупный | `scale_large` | × 1.20 | × 1.15 | × 1.10 |

### Реализация через DynamicResource

Все значения шрифтов в стилях Typography.xaml уже используют `DynamicResource`.
При переключении пресета вызывается `ThemeService.ApplyScale(ScalePreset)`:

```csharp
public static void ApplyScale(ScalePreset preset)
{
    double factor = preset switch {
        ScalePreset.Small   => 0.85,
        ScalePreset.Large   => 1.20,
        _                   => 1.00
    };
    // Базовые размеры из Typography.xaml умножаются на factor
    Application.Current!.Resources["TextBase"]    = Math.Round(15 * factor);
    Application.Current!.Resources["TextSm"]      = Math.Round(13 * factor);
    Application.Current!.Resources["TextXs"]      = Math.Max(12, Math.Round(12 * factor));
    Application.Current!.Resources["TextLg"]      = Math.Round(18 * factor);
    Application.Current!.Resources["TextXl"]      = Math.Round(24 * factor);
    Application.Current!.Resources["TextValueMd"] = Math.Round(28 * factor);
    Application.Current!.Resources["TextValueLg"] = Math.Round(40 * factor);
    Application.Current!.Resources["TextValueXl"] = Math.Round(56 * factor);
    // Spacing
    double sf = preset switch { ScalePreset.Small => 0.90, ScalePreset.Large => 1.10, _ => 1.00 };
    Application.Current!.Resources["Spacing16"] = Math.Round(16 * sf);
    Application.Current!.Resources["Spacing20"] = Math.Round(20 * sf);
    Application.Current!.Resources["Spacing24"] = Math.Round(24 * sf);
    Application.Current!.Resources["PaddingPage"] = new Thickness(Math.Round(20 * sf));
    // Сохранить выбор
    Preferences.Set("ui_scale", (int)preset);
}
```

**Ограничение:** `TextXs` никогда не падает ниже 12sp, даже при `scale_small`.

### UI переключателя в настройках

```
┌──────────────────────────────────┐
│  Размер интерфейса               │
│                                  │
│  [А-]  [А]  [А+]                 │
│  Мал.  Ст.  Кр.                  │
│                                  │
│  Предпросмотр:                   │
│  ┌──────────────────────────┐    │
│  │  7.4 ммоль/л             │    │
│  │  Норма  ↑ 0.3            │    │
│  └──────────────────────────┘    │
└──────────────────────────────────┘
```

Три кнопки: `GhostButton` для неактивных, `PrimaryButton` для активного.
Предпросмотр — `DataCard` с live-обновлением при переключении.

---

## §14. Анимации

Все анимации уважают `Accessibility.IsReduceMotionEnabled`:
```csharp
if (Accessibility.IsReduceMotionEnabled) return; // пропустить анимацию
```

| Элемент | Анимация | Duration | Easing |
|---------|----------|----------|--------|
| Появление карточки | FadeIn + TranslateY(+16→0) | 220ms | CubicOut |
| Нажатие кнопки | Scale 1.0→0.985→1.0 | 120ms | Linear |
| Смена экрана | Slide (или Fade) | 250ms | CubicInOut |
| Смена темы | CrossFade | 300ms | Linear |
| Пульс (pending sync dot) | Opacity 1.0→0.4→1.0 | 1200ms | SinIn, repeat ∞ |
| Shake (CodeInput, ошибка) | TranslateX: -8,+8,-6,+6,-4,+4,0 | 400ms | Linear |
| Загрузка числа (KPI) | Count-up от предыдущего значения | 600ms | CubicOut |
| Skeleton shimmer | BackgroundColor sweep | 1500ms | Linear, repeat ∞ |
| Bottom Sheet slide-up | TranslateY(height→0) | 280ms | CubicOut |
| Dialog appear | Opacity(0→1) + Scale(0.92→1.0) | 200ms | CubicOut |

### Skeleton loader

Применяется при первой загрузке данных страницы (до первого ответа API).

```xaml
<BoxView
    HeightRequest="20" CornerRadius="8"
    Color="{DynamicResource SurfaceOffset}"
    x:Name="SkeletonLine" />
<!-- shimmer: анимировать Color между SurfaceOffset и SurfaceElevated -->
```

---

## §15. Вход и восстановление пароля (NEW v2)

### Экран входа

```
┌────────────────────────────────┐
│           SugarGuard           │ ← логотип + слоган, по центру
│        «Контроль диабета»      │
│                                │
│  ┌──────────────────────────┐  │
│  │  Email                   │  │  ← EntryText
│  └──────────────────────────┘  │
│  ┌──────────────────────────┐  │
│  │  Пароль             [👁] │  │
│  └──────────────────────────┘  │
│                                │
│  [          Войти           ]  │ ← PrimaryButton, full-width
│                                │
│  ──────────── или ────────────  │
│                                │
│  [    Войти через Яндекс   ]   │ ← SecondaryButton, иконка Яндекс
│                                │
│  Забыли пароль?                │ ← GhostButton, TextSecondary
│  Нет аккаунта? Создать         │ ← inline, TextPrimary / Primary
└────────────────────────────────┘
```

- Фон: `BackgroundPage`
- Логотип: 80×80dp, центрирован, `Margin = 0,64,0,40`
- Rate limit feedback: после 5 неудачных попыток показывается `StateBannerWarning`
  «Слишком много попыток. Подождите X минут.»
- При `IsEmailVerified = false`: после входа автоматически переход на экран верификации.

### Восстановление пароля

Три шага в одном экране (только активный шаг видим):

1. **Email**: поле email, кнопка «Отправить код». После отправки — `StateBannerSuccess`.
2. **Код**: `CodeInput` (6 боксов) + таймер «Отправить повторно через X сек».
3. **Новый пароль**: два поля (пароль + подтверждение), индикатор сложности, кнопка «Сохранить».

---

## §16. Ограниченный режим (до верификации) (NEW v2)

После регистрации, до подтверждения email/телефона.

### Баннер ограниченного режима

Постоянная полоска под AppBar (не закрывается):

```
┌──────────────────────────────────────────────────┐
│  📧  Подтвердите email для доступа ко всем функциям  [→] │
│      цвет: GlucoseWarning (жёлтый)                       │
└──────────────────────────────────────────────────┘
```

### Заблокированные функции

При попытке использовать заблокированную функцию:
- Bottom Sheet с объяснением: «Экспорт доступен после подтверждения email»
- Кнопка «Подтвердить сейчас» — переходит на экран верификации

| Функция | В ограниченном режиме |
|---------|----------------------|
| Ввод измерений | ✅ Доступно |
| Рюкзак / перекусы | ✅ Доступно |
| Рекомендации ИИ | ✅ Доступно |
| Расписание | ✅ Доступно |
| Экспорт PDF / CSV | 🔒 Заблокировано |
| Привязка родителя | 🔒 Заблокировано |
| Привязка врача | 🔒 Заблокировано |
| Telegram-бот | 🔒 Заблокировано |

---

## §17. Регистрация, верификация, онбординг (NEW v2)

### 17.1. Экран регистрации ребёнка

Шаги расположены вертикально, прокручиваются.

**Шаг 1 — Способ:**
```
┌───────────────────────────────┐
│        Создать аккаунт        │ ← PageTitleText, centered
│                               │
│  [    Зарегистрироваться   ]  │ ← PrimaryButton
│  [   Войти через Яндекс   ]  │ ← SecondaryButton, иконка
└───────────────────────────────┘
```

**Шаг 2 — Данные:**

Поля: Email (EntryText), Пароль (с глазком + индикатор сложности), Дата рождения (DatePicker).

Индикатор сложности пароля:
```
Слабый   [████░░░░]  GlucoseDanger
Средний  [████████]  GlucoseWarning
Сильный  [████████]  GlucoseNormal
```
3 сегмента `BoxView`, ширина пропорциональна силе, высота 4dp, radius 2.

Если возраст < 14 лет — появляется `DataCard` с чекбоксом согласия родителя (152-ФЗ).

**Шаг 3 — Выбор канала верификации:**
```
Выберите способ подтверждения:
○  Email (адрес@пример.com)
○  SMS  (+7 ...)
```
`RadioButton`, стиль через `AppThemeBinding`.

**Общие правила:**
- Кнопка «Далее» / «Зарегистрироваться» неактивна (`Disabled`) до заполнения обязательных полей.
- Ошибки валидации — inline, под полем, `DangerText`, 12sp.
- При ошибке API — `StateBannerError` вверху.

### 17.2. Экран верификации (CodeInput)

```
┌──────────────────────────────────┐
│  Подтвердите email               │ ← PageTitleText
│  Код отправлен на user@mail.com  │ ← BodyTextMuted
│                                  │
│  ┌───┐┌───┐┌───┐  ┌───┐┌───┐┌───┐│ ← CodeInput (§10)
│  │   ││   ││   │  │   ││   ││   ││
│  └───┘└───┘└───┘  └───┘└───┘└───┘│
│                                  │
│        [  Подтвердить  ]         │ ← PrimaryButton (disabled до 6 цифр)
│                                  │
│  Отправить повторно через 42 с   │ ← CaptionText + countdown timer
│  или  [Изменить email]           │ ← GhostButton
└──────────────────────────────────┘
```

Таймер повторной отправки: 120 секунд. После истечения — кнопка «Отправить повторно» активна.

При ошибке кода:
- Все 6 боксов: `InputBorderError`
- Shake-анимация (§14)
- Текст под CodeInput: «Неверный код. Осталось X попыток.» в `DangerText`

### 17.3. Онбординг (после первой верификации)

3 шага, progress bar вверху (3 сегмента).

```
Шаг 1: Имя / никнейм
Шаг 2: Тип диабета (1 / 2 / Другой) — SegmentedControl
Шаг 3: Целевой диапазон (ползунок Min–Max ммоль/л)
```

**SegmentedControl** (тип диабета):
```
┌─────────┬─────────┬─────────┐
│  Тип 1  │  Тип 2  │  Другой │
└─────────┴─────────┴─────────┘
```
Реализация: `Border` + `Grid` с 3 `Button`. Активная ячейка: `PrimaryButton`, неактивные: `SoftButton`.
Высота: 48dp, radius 18, без внешней обводки у контейнера.

**Ползунок целевого диапазона:**
- `Slider` в MAUI не поддерживает range нативно → два наложенных `Slider` или custom `RangeSlider`.
- Min: 3.5–6.0, Max: 7.0–14.0. Шаг 0.1.
- Цвет трека: `ProgressTrackBackground`. Заполнение: `Primary`.
- Метки значений: ClashDisplay Bold, 18sp, над ползунками.

Кнопка «Пропустить»: `GhostButton`, `TextSecondary`, выровнена по правому краю AppBar.

---

## §18. Связки «Ребёнок — Родитель / Врач» (NEW v2)

### 18.1. Экран управления связками (приложение, ребёнок)

Расположен в «Настройки → Доступ».

```
┌──────────────────────────────────┐
│  Доступ к моим данным            │ ← PageTitleText
│                                  │
│  РОДИТЕЛИ                        │ ← SectionHeadingTextMuted, uppercase
│  ┌────────────────────────────┐  │
│  │  👤 Мама (Иванова Н.А.)   │  │ ← ListCard
│  │     Привязан 03.05.2026    │  │    TextSecondary, 12sp
│  │                   [Отвяз.] │  │    GhostButton, DangerText
│  └────────────────────────────┘  │
│  [+ Пригласить родителя]         │ ← SoftButton
│                                  │
│  ВРАЧ                            │
│  (пусто)                         │ ← EmptyStateView Small
│  [+ Привязать врача]             │ ← SoftButton
└──────────────────────────────────┘
```

### 18.2. Генерация кода приглашения

Bottom Sheet, появляется по нажатию «+ Пригласить родителя»:

```
┌──────────────────────────────────┐
│  ──── (handle)                   │
│  Код для родителя                │ ← SectionHeadingText
│                                  │
│  ┌────────────────────────────┐  │
│  │     A4K9-RT2B              │  │ ← HeroCard, ClashDisplay Bold 32sp
│  │   Действует 48 часов       │  │    CaptionText, TextSecondary
│  └────────────────────────────┘  │
│                                  │
│  [  Поделиться кодом  ]          │ ← PrimaryButton
│  [     Закрыть        ]          │ ← GhostButton
└──────────────────────────────────┘
```

### 18.3. Входящий запрос на связку (плашка в приложении)

Появляется как `StateBanner` поверх контента при наличии `IncomingLinkRequest`:

```
┌──────────────────────────────────────────────────┐
│  👤  Иванова Надежда хочет получить доступ       │
│      к вашим данным как родитель                 │
│                                                  │
│  [  Подтвердить  ]    [  Отклонить  ]            │
│  (PrimaryButton)       (GhostButton)             │
└──────────────────────────────────────────────────┘
```

- Цвет фона: `SurfacePrimarySoft`
- Граница: `Primary`
- Не исчезает автоматически — требует явного ответа
- При нажатии «Подтвердить»: `StateBannerSuccess` «Связка создана»
- При нажатии «Отклонить»: `StateBannerWarning` «Запрос отклонён»

### 18.4. Подтверждение разрыва связки

`Confirmation Dialog` (§11):
- Заголовок: «Отвязать [Имя]?»
- Текст: «[Имя] больше не будет видеть ваши данные»
- Кнопка подтверждения: `Destructive`

---

## §19. Web UI Kit — Blazor (NEW v2)

Web-интерфейс (SugarGuard.Web) использует те же токены дизайна что и MAUI,
реализованные через CSS-переменные в `wwwroot/css/variables.css`.

### 19.1. CSS-переменные

```css
/* variables.css — Light (default) */
:root {
  /* Поверхности */
  --color-bg:             #F4F7FB;
  --color-surface:        rgba(255,255,255,0.85);
  --color-surface-2:      rgba(255,255,255,0.95);
  --color-surface-offset: #E9EFF9;
  --color-border:         rgba(22,33,62,0.10);
  --color-divider:        rgba(22,33,62,0.08);

  /* Текст */
  --color-text:           #16213E;
  --color-text-muted:     #667694;
  --color-text-faint:     #96A2B8;
  --color-text-inverse:   #FFFFFF;

  /* Бренд */
  --color-primary:        #1B8E8B;
  --color-primary-hover:  #56D0BF;
  --color-primary-alt:    #2678D9;

  /* Глюкоза */
  --color-glucose-normal:  #37A563;
  --color-glucose-warning: #E3A32B;
  --color-glucose-danger:  #DB5967;

  /* Радиусы */
  --radius-sm:  14px;
  --radius-md:  18px;
  --radius-lg:  24px;
  --radius-xl:  32px;
  --radius-full: 9999px;

  /* Отступы */
  --space-4:  4px;   --space-8:  8px;
  --space-12: 12px;  --space-16: 16px;
  --space-20: 20px;  --space-24: 24px;
  --space-32: 32px;  --space-40: 40px;
  --space-48: 48px;  --space-64: 64px;

  /* Тени */
  --shadow-card: 0 12px 28px rgba(22,33,62,0.07);
  --shadow-hero: 0 24px 48px rgba(22,33,62,0.11);
  --shadow-overlay: 0 32px 90px rgba(22,33,62,0.16);

  /* Шрифты */
  --font-display: 'ClashDisplay', 'Satoshi', sans-serif;
  --font-body:    'Satoshi', system-ui, sans-serif;

  /* Переходы */
  --transition: 180ms cubic-bezier(0.16, 1, 0.3, 1);
}

/* Dark mode */
[data-theme="dark"] {
  --color-bg:             #0B1018;
  --color-surface:        rgba(17,24,37,0.80);
  --color-surface-2:      rgba(17,24,37,0.90);
  --color-surface-offset: #10192A;
  --color-border:         rgba(237,244,255,0.09);
  --color-divider:        rgba(237,244,255,0.08);
  --color-text:           #EDF4FF;
  --color-text-muted:     #9EAED0;
  --color-text-faint:     #6F7D99;
  --color-primary:        #56D0BF;
  --color-primary-hover:  #1B8E8B;
  --color-primary-alt:    #6DAEFF;
  --color-glucose-normal:  #62D889;
  --color-glucose-warning: #F4BC56;
  --color-glucose-danger:  #FF7A8B;
  --shadow-card: 0 12px 28px rgba(0,0,0,0.28);
  --shadow-hero: 0 24px 58px rgba(0,0,0,0.34);
  --shadow-overlay: 0 36px 100px rgba(0,0,0,0.48);
}
```

### 19.2. Типографика (Web)

```css
h1 { font-family: var(--font-display); font-size: clamp(24px, 3vw, 32px); font-weight: 700; }
h2 { font-family: var(--font-display); font-size: 20px; font-weight: 700; }
h3 { font-family: var(--font-body); font-size: 16px; font-weight: 600; }
body, p, li { font-family: var(--font-body); font-size: 15px; line-height: 1.5; }
.caption { font-size: 12px; color: var(--color-text-muted); }
.label-xs { font-size: 11px; font-weight: 600; letter-spacing: 0.04em; text-transform: uppercase; }
```

Минимальный размер — 12px. Тело — 15px.

### 19.3. Компоненты Web

#### Кнопки

```css
.btn { min-height: 44px; border-radius: var(--radius-md); padding: 10px 20px;
       font-family: var(--font-body); font-size: 14px; font-weight: 600;
       transition: all var(--transition); cursor: pointer; }
.btn-primary  { background: var(--color-primary); color: white; border: none; }
.btn-primary:hover { background: var(--color-primary-hover); transform: translateY(-1px); }
.btn-secondary { background: transparent; color: var(--color-primary);
                 border: 1.5px solid var(--color-primary); }
.btn-ghost    { background: transparent; color: var(--color-text-muted); border: none; }
.btn-danger   { background: var(--color-glucose-danger); color: white; border: none; }
```

#### Карточки

```css
.card {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  padding: var(--space-20);
  box-shadow: var(--shadow-card);
  transition: box-shadow var(--transition);
}
.card-hero {
  background: linear-gradient(135deg,
    rgba(244,255,255,0.95) 0%,
    rgba(27,142,139,0.13) 55%,
    rgba(38,120,217,0.10) 100%);
  border-color: rgba(27,142,139,0.15);
  box-shadow: var(--shadow-hero);
}
```

#### KPI-блок (MetricCard Web)

```html
<div class="kpi-card card">
  <div class="kpi-label">TIR за неделю</div>
  <div class="kpi-value">78<span class="kpi-unit">%</span></div>
  <div class="kpi-delta positive">↑ 4% vs прошлая неделя</div>
</div>
```

```css
.kpi-value { font-family: var(--font-display); font-size: 36px; font-weight: 700;
             color: var(--color-text); }
.kpi-unit  { font-size: 18px; font-weight: 400; color: var(--color-text-muted); }
.kpi-delta.positive { color: var(--color-glucose-normal); font-size: 13px; }
.kpi-delta.negative { color: var(--color-glucose-danger); font-size: 13px; }
```

#### Поля ввода (Web)

```css
.input-field {
  width: 100%; min-height: 48px; padding: 12px 16px;
  background: var(--color-surface-2);
  border: 1.5px solid var(--color-border);
  border-radius: var(--radius-sm);
  font-family: var(--font-body); font-size: 15px; color: var(--color-text);
  transition: border-color var(--transition);
}
.input-field:focus { border-color: var(--color-primary); outline: none;
                     box-shadow: 0 0 0 3px rgba(27,142,139,0.12); }
.input-field.error { border-color: var(--color-glucose-danger); }
.input-label { font-size: 13px; font-weight: 600; color: var(--color-text-muted);
               margin-bottom: 6px; display: block; }
.input-helper { font-size: 12px; color: var(--color-text-faint); margin-top: 4px; }
.input-helper.error { color: var(--color-glucose-danger); }
```

#### Таблица (Dashboard, список пациентов)

```css
.data-table { width: 100%; border-collapse: collapse; }
.data-table th { font-size: 12px; font-weight: 600; text-transform: uppercase;
                 letter-spacing: 0.04em; color: var(--color-text-muted);
                 padding: 10px 16px; text-align: left; border-bottom: 1px solid var(--color-divider); }
.data-table td { padding: 14px 16px; font-size: 14px; color: var(--color-text);
                 border-bottom: 1px solid var(--color-divider); }
.data-table tr:hover td { background: var(--color-surface-offset); }
/* Числовые колонки: tabular-nums */
.data-table .col-number { font-variant-numeric: tabular-nums; text-align: right; }
```

### 19.4. Макет страниц (Web)

#### Общий шаблон (Sidebar + Content)

```
┌──────────────────────────────────────────────────────┐
│  [Логотип]    SugarGuard                  [Тема] [👤] │ ← TopNav, h=60px
├────────────┬─────────────────────────────────────────┤
│            │                                         │
│  НАВИГАЦИЯ │  КОНТЕНТ                                │
│  (220px)   │  (flex 1)                               │
│            │  ┌─────────────────────────────────┐    │
│  Дашборд   │  │  PageTitle                      │    │
│  Данные    │  │  [StateBanner (если нужен)]     │    │
│  Настройки │  │  ...content...                  │    │
│            │  └─────────────────────────────────┘    │
│            │                                         │
└────────────┴─────────────────────────────────────────┘
```

На мобильных (< 768px): sidebar → hamburger-меню + slide-in drawer.

#### Каноническая навигация родителя

Sidebar и верхняя tab-панель кабинета родителя используют один и тот же порядок и названия:

1. `Обзор`
2. `Измерения`
3. `Уведомления`
4. `Рюкзак`
5. `Дневник питания`
6. `Записки врача`
7. `Профиль ребёнка`
8. `Настройки`

Если раздел открыт отдельной страницей (`/parent/nutrition`, `/parent/access`), пункт sidebar остаётся в той же группе, а topbar не должен показывать альтернативное название. Бейджи в sidebar, topbar и колокольчике должны считаться из одного источника состояния.

#### Кабинет родителя — Дашборд

```
┌──────────────────────────────────────────────────────┐
│  Добрый вечер, Надежда               [Выбор ребёнка ▾]│
│                                                      │
│  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────────┐│
│  │  7.4     │ │  TIR     │ │  Алерты  │ │  Тренд   ││
│  │  ммоль/л │ │  78%     │ │  2/нед   │ │  ↓ норм  ││
│  └──────────┘ └──────────┘ └──────────┘ └──────────┘│
│  (MetricCard × 4, .card-hero для первой)             │
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  График глюкозы за 24 часа         [1д 3д 7д]  │  │
│  │  [Chart.js, area-fill]                         │  │
│  └────────────────────────────────────────────────┘  │
│                                                      │
│  ┌──────────────────────┐ ┌─────────────────────┐   │
│  │  Последние события   │ │  Рюкзак             │   │
│  │  (хронология)        │ │  7.5 ХЕ доступно    │   │
│  └──────────────────────┘ └─────────────────────┘   │
└──────────────────────────────────────────────────────┘
```

#### Кабинет родителя — Дневник питания

Экран дневника питания и инсулина строится в том же стиле, что и основной дашборд:

- вверху компактные KPI-карточки: `ХЕ за день/период`, `Инсулин`, `Записей`, `Выполнение расписания`;
- блок записей дневника: время, тип приёма, ХЕ, инсулин, заметка, источник (`ребёнок` / `родитель`);
- блок расписания: редактируемые приёмы пищи и перекусы, время, напоминания, активность;
- блок динамики: график ХЕ и инсулина, таблица, экспорт CSV/PDF.

Карточки дневника не должны падать в нативный HTML-вид без классов. Любой новый раздел кабинета родителя обязан использовать общий layout, `card`, `btn`, `metric-card`, `data-table` и adaptive grid.

#### Кабинет родителя — Управление доступом

Экран доступа использует карточную структуру:

- `Родитель` и `Врач` — две равные action-карточки с описанием прав, активным кодом и CTA `Выдать код`.
- `Текущие связи` — список связанных пользователей с ролью, датой привязки и действием `Отвязать`.
- `Активировать код` — отдельная card/input/action строка.

Не допускаются формы на всю ширину без отступов, кнопки поверх input и неодинаковые высоты карточек в одном ряду.

#### Кабинет врача — Список пациентов

```
┌──────────────────────────────────────────────────────┐
│  Мои пациенты                    [+ Привязать пациента]│
│                                                      │
│  ┌────────────────────────────────────────────────┐  │
│  │  Имя           TIR 7д    Посл. измер.  Статус  │  │
│  │  ─────────────────────────────────────────────  │  │
│  │  Петров И.     78%       1ч назад      Норма   │  │
│  │  Сидорова М.   52%       3ч назад      Внимание│  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

Статус — `StatusChip` с соответствующим цветом.

#### Панель администратора

```
┌──────────────────────────────────────────────────────┐
│  Администрирование                                   │
│                                                      │
│  [Пользователи] [Заявки врачей] [Аудит] [Система]   │ ← TabBar-стиль
│                                                      │
│  Заявки врачей на подтверждение (3)                  │
│  ┌────────────────────────────────────────────────┐  │
│  │ ФИО           Специализация   Место работы     │  │
│  │ [Подтвердить] [Отклонить]                      │  │
│  └────────────────────────────────────────────────┘  │
└──────────────────────────────────────────────────────┘
```

### 19.5. Графики (Web, Chart.js)

Настройки по умолчанию для всех графиков:

```javascript
const chartDefaults = {
  responsive: true,
  maintainAspectRatio: false,
  plugins: {
    legend: { display: false },
    tooltip: { backgroundColor: 'var(--color-surface-2)', titleColor: 'var(--color-text)',
               bodyColor: 'var(--color-text-muted)', borderColor: 'var(--color-border)',
               borderWidth: 1, padding: 12, cornerRadius: 12 }
  },
  scales: {
    x: { grid: { color: 'var(--color-divider)' },
         ticks: { color: 'var(--color-text-faint)', font: { family: 'Satoshi', size: 11 } } },
    y: { grid: { color: 'var(--color-divider)' },
         ticks: { color: 'var(--color-text-faint)', font: { family: 'Satoshi', size: 11 } } }
  }
};
```

Area-fill для графика глюкозы:

```javascript
const glucoseColor = '#37A563'; // или взять из текущего статуса
datasets: [{
  data: glucoseData,
  borderColor: glucoseColor,
  borderWidth: 2,
  fill: true,
  backgroundColor: (ctx) => {
    const gradient = ctx.chart.ctx.createLinearGradient(0, 0, 0, ctx.chart.height);
    gradient.addColorStop(0, glucoseColor + '47');  // opacity ~28%
    gradient.addColorStop(1, glucoseColor + '00');  // opacity 0%
    return gradient;
  },
  tension: 0.4,  // spline
  pointRadius: 0,
  pointHoverRadius: 5
}]
```

Зоны диапазона добавляются через Chart.js annotation plugin.

---

## §20. Accessibility (общие требования)

| Правило | Мобильное (MAUI) | Web (Blazor) |
|---------|-----------------|--------------|
| Контраст текст/фон | ≥ 4.5:1 основной | ≥ 4.5:1 |
| Минимальная область касания | 52×52 dp | 44×44 px (WCAG) |
| Screen reader | `SemanticProperties.Description` | `aria-label`, `role` |
| Заголовки | `SemanticHeadingLevel` | `<h1>...<h3>` иерархия |
| Состояния кнопок | `IsEnabled + Disabled VisualState` | `disabled` + CSS |
| Фокус | нативный MAUI | `:focus-visible` |
| Цвет не единственный индикатор | chip = цвет + текст | chip = цвет + текст |
| Reduced motion | `Accessibility.IsReduceMotionEnabled` | `prefers-reduced-motion` |

---

## §21. Шаблон новой страницы (чеклист)

При создании нового экрана (MAUI или Blazor) проверить:

- [ ] Фон: `BackgroundPage` / `--color-bg`
- [ ] Заголовок страницы: `PageTitleText` / `h1`
- [ ] Все поля ввода: `EntryText` + `InputBorderColor` + helper-text
- [ ] Основная кнопка: одна, `PrimaryButton`, full-width (мобильное) или 200px min (web)
- [ ] Состояния загрузки: `StateBannerLoading` или skeleton
- [ ] Пустое состояние: `EmptyStateView` с понятным текстом и CTA
- [ ] Ошибка: `StateBannerError` или inline под полем
- [ ] Деструктивные действия: `Confirmation Dialog`
- [ ] `SemanticProperties` / `aria-*` на кнопках-иконках
- [ ] Тёмная тема: проверить все цвета через `AppThemeBinding` / `[data-theme=dark]`
- [ ] Child Mode (MAUI): все размеры через `DynamicResource`
- [ ] Анимации: уважают `IsReduceMotionEnabled`
- [ ] Pending sync dot (если экран содержит `Measurement` / `BackpackItem`)
