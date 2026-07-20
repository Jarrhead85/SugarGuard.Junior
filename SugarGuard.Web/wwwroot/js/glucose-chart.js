/**
 * @fileoverview SugarGuard — wwwroot/js/glucose-chart.js
 *
 * Единый JS-модуль для всех графиков Chart.js в проекте.
 * Заменяет два отдельных файла: glucoseChart.js (window.sgGlucoseChart)
 * и charts.js (window.SugarGuardCharts), которые дублировали логику.
 *
 * Экспортирует ДВА глобальных объекта для обратной совместимости:
 *   window.sgGlucoseChart   — используется GlucoseChart.razor (JS Interop)
 *   window.SugarGuardCharts — используется Dashboard.razor и legacy-кодом
 *
 * Зависимости (подключить ДО этого файла в App.razor / _Host.cshtml):
 *   1. https://cdn.jsdelivr.net/npm/chart.js@4.4.3/dist/chart.umd.min.js
 *   2. https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js
 *
 * Порядок в App.razor:
 *   <script src="https://cdn.jsdelivr.net/npm/chart.js@4.4.3/dist/chart.umd.min.js"></script>
 *   <script src="https://cdn.jsdelivr.net/npm/chartjs-plugin-annotation@3.0.1/dist/chartjs-plugin-annotation.min.js"></script>
 *   <script src="js/glucose-chart.js"></script>
 *   <script src="js/theme.js"></script>
 *   <script src="js/tokenStore.js"></script>
 *
 * @version 2.0.0
 * @see UIKit v2.0 §8 (графики), §19.5 (Chart.js интеграция)
 */

(function (window) {
    'use strict';

    /* ================================================================
     * 1. КОНСТАНТЫ ГЛЮКОЗЫ
     * Соответствуют GlucoseLevels в SugarGuard.Shared.Constants.
     * ================================================================ */

    /** @type {number} Нижняя граница оси Y */
    var GLUCOSE_Y_MIN = 2.0;

    /** @type {number} Верхняя граница оси Y */
    var GLUCOSE_Y_MAX = 16.0;

    /** @type {number} Критически низкий уровень (<3.0) */
    var GLUCOSE_CRITICAL_LOW = 3.0;

    /** @type {number} Нижняя граница нормы (4.0) */
    var GLUCOSE_LOW = 4.0;

    /** @type {number} Верхняя граница нормы (10.0) */
    var GLUCOSE_HIGH = 10.0;

    /** @type {number} Критически высокий уровень (>14.0) */
    var GLUCOSE_CRITICAL_HIGH = 14.0;

    /**
     * Запасные цвета на случай, если CSS-переменные ещё не загружены.
     * Значения строго из UIKit v2.0 §2 (GlucoseNormal/Warning/Danger).
     * @type {Object}
     */
    var FALLBACK_COLORS = {
        normal: { light: '#37A563', dark: '#62D889' },
        warning: { light: '#E3A32B', dark: '#F4BC56' },
        danger: { light: '#DB5967', dark: '#FF7A8B' },
        primary: { light: '#1B8E8B', dark: '#56D0BF' }
    };

    /* ================================================================
     * 2. РЕЕСТР ИНСТАНЦИЙ Chart.js
     * ================================================================ */

    /** @type {Map<string, Chart>} canvasId → Chart instance */
    var instances = new Map();

    /* ================================================================
     * 3. ВСПОМОГАТЕЛЬНЫЕ ФУНКЦИИ — CSS, ЦВЕТ
     * ================================================================ */

    /**
     * Читает значение CSS-переменной с :root / html.
     * Безопасен при SSR-пререндере (document может отсутствовать).
     * @param {string} name - имя переменной, например '--color-glucose-normal'
     * @param {string} [fallback=''] - значение по умолчанию
     * @returns {string}
     */
    function cssVar(name, fallback) {
        fallback = fallback || '';
        try {
            var val = getComputedStyle(document.documentElement)
                .getPropertyValue(name)
                .trim();
            return val || fallback;
        } catch (e) {
            return fallback;
        }
    }

    /**
     * Определяет активную тему (light/dark).
     * Сначала проверяет window.SugarGuardTheme (theme.js),
     * затем data-theme на <html>, затем prefers-color-scheme.
     * @returns {boolean} true если тёмная тема
     */
    function isDarkTheme() {
        if (typeof window.SugarGuardTheme !== 'undefined' &&
            typeof window.SugarGuardTheme.isDark === 'function') {
            return window.SugarGuardTheme.isDark();
        }
        var attr = document.documentElement.getAttribute('data-theme');
        if (attr === 'dark') return true;
        if (attr === 'light') return false;
        return window.matchMedia &&
            window.matchMedia('(prefers-color-scheme: dark)').matches;
    }

    /**
     * Возвращает цвет глюкозного статуса из CSS-переменных.
     * При недоступности CSS-переменных использует FALLBACK_COLORS.
     * @param {'normal'|'warning'|'danger'|'primary'} state
     * @returns {string} hex-цвет
     */
    function getGlucoseColor(state) {
        var varMap = {
            normal: '--color-glucose-normal',
            warning: '--color-glucose-warning',
            danger: '--color-glucose-danger',
            primary: '--color-primary'
        };
        var varName = varMap[state] || varMap['normal'];
        var dark = isDarkTheme();
        var fb = (FALLBACK_COLORS[state] || FALLBACK_COLORS.normal);
        return cssVar(varName, dark ? fb.dark : fb.light);
    }

    /**
     * Преобразует hex-цвет в rgba() строку с заданной прозрачностью.
     * Поддерживает форматы #RGB и #RRGGBB.
     * @param {string} hex - например '#37A563' или '37A563'
     * @param {number} alpha - 0.0–1.0
     * @returns {string}
     */
    function hexToRgba(hex, alpha) {
        if (!hex || typeof hex !== 'string') {
            return 'rgba(0,0,0,' + alpha + ')';
        }
        var h = hex.replace('#', '');
        if (h.length === 3) {
            h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
        }
        if (h.length !== 6) {
            return 'rgba(0,0,0,' + alpha + ')';
        }
        var r = parseInt(h.substring(0, 2), 16);
        var g = parseInt(h.substring(2, 4), 16);
        var b = parseInt(h.substring(4, 6), 16);
        return 'rgba(' + r + ',' + g + ',' + b + ',' + alpha + ')';
    }

    /**
     * Строит area-fill градиент для линейного графика глюкозы.
     * opacity: 0.28 (top) → 0.00 (bottom) — по UIKit v2.0 §8.
     * @param {CanvasRenderingContext2D} ctx
     * @param {number} chartHeight - высота области графика в пикселях
     * @param {string} color - hex-цвет
     * @returns {CanvasGradient}
     */
    function buildAreaGradient(ctx, chartHeight, color) {
        var gradient = ctx.createLinearGradient(0, 0, 0, chartHeight);
        gradient.addColorStop(0, hexToRgba(color, 0.28));
        gradient.addColorStop(1, hexToRgba(color, 0.00));
        return gradient;
    }

    /**
     * Определяет, зарегистрирован ли плагин аннотаций в Chart.js.
     * Поддерживает Chart.js 4.x и старые версии.
     * @returns {boolean}
     */
    function hasAnnotationPlugin() {
        if (typeof window.Chart === 'undefined') return false;
        // CDN подключает плагин через window['chartjs-plugin-annotation']
        if (window['chartjs-plugin-annotation']) return true;
        // Chart.js 4.x: проверяем через registry
        try {
            return !!(window.Chart.registry &&
                window.Chart.registry.plugins &&
                window.Chart.registry.plugins.get('annotation'));
        } catch (e) {
            return false;
        }
    }

    /**
     * Безопасно парсит JSON-строку.
     * Если значение уже не строка — возвращает как есть.
     * @param {*} value
     * @returns {*}
     */
    function safeParse(value) {
        if (value === null || value === undefined) return null;
        if (typeof value !== 'string') return value;
        try {
            return JSON.parse(value);
        } catch (e) {
            console.warn('[SugarGuard Charts] JSON parse error:', e, value);
            return null;
        }
    }

    /**
     * Получает canvas-элемент по ID.
     * @param {string} canvasId
     * @returns {HTMLCanvasElement|null}
     */
    function getCanvas(canvasId) {
        var el = document.getElementById(canvasId);
        return (el instanceof HTMLCanvasElement) ? el : null;
    }

    /**
     * Уничтожает инстанцию Chart.js по ID canvas, если она существует.
     * Перехватывает ошибки при Blazor-навигации (canvas уже вне DOM).
     * @param {string} canvasId
     */
    function destroyById(canvasId) {
        if (instances.has(canvasId)) {
            try {
                instances.get(canvasId).destroy();
            } catch (e) {
                // canvas уже удалён из DOM при Blazor-навигации
            }
            instances.delete(canvasId);
        }
    }

    /* ================================================================
     * 4. ПОСТРОИТЕЛИ КОНФИГУРАЦИИ Chart.js
     * ================================================================ */

    /**
     * Строит конфигурацию тултипа по UIKit v2.0 §19.5.
     * Читает цвета из CSS-переменных в момент вызова.
     * @returns {object}
     */
    function buildTooltipConfig() {
        return {
            backgroundColor: cssVar('--color-surface-2', '#ffffff'),
            titleColor: cssVar('--color-text', '#16213E'),
            bodyColor: cssVar('--color-text-muted', '#667694'),
            borderColor: cssVar('--color-border', 'rgba(22,33,62,0.10)'),
            borderWidth: 1,
            padding: 12,
            cornerRadius: 12,
            callbacks: {
                label: function (ctx) {
                    return ctx.parsed.y.toFixed(1) + ' ммоль/л';
                }
            }
        };
    }

    /**
     * Строит конфигурацию оси X.
     * @param {boolean} hide - скрыть ось полностью
     * @returns {object}
     */
    function buildXScale(hide) {
        var gridColor = cssVar('--color-divider', 'rgba(22,33,62,0.07)');
        var tickColor = cssVar('--color-text-faint', '#96A2B8');
        var fontFamily = cssVar('--font-body', 'Satoshi, system-ui, sans-serif');
        return {
            display: !hide,
            grid: {
                color: gridColor,
                drawBorder: false,
                drawTicks: false
            },
            ticks: {
                color: tickColor,
                font: { family: fontFamily, size: 11 },
                maxRotation: 0,
                maxTicksLimit: 8
            },
            border: { display: false }
        };
    }

    /**
     * Строит конфигурацию оси Y.
     * @param {number} yMin
     * @param {number} yMax
     * @param {boolean} hide - скрыть ось полностью
     * @returns {object}
     */
    function buildYScale(yMin, yMax, hide) {
        var gridColor = cssVar('--color-divider', 'rgba(22,33,62,0.07)');
        var tickColor = cssVar('--color-text-faint', '#96A2B8');
        var fontFamily = cssVar('--font-body', 'Satoshi, system-ui, sans-serif');
        return {
            display: !hide,
            grid: {
                color: gridColor,
                drawBorder: false,
                drawTicks: false
            },
            ticks: {
                color: tickColor,
                font: { family: fontFamily, size: 11 },
                callback: function (v) { return Number(v).toFixed(1); },
                maxTicksLimit: 6
            },
            border: { display: false },
            min: yMin !== undefined ? yMin : GLUCOSE_Y_MIN,
            max: yMax !== undefined ? yMax : GLUCOSE_Y_MAX
        };
    }

    /**
     * Строит аннотации зон глюкозы по UIKit v2.0 §8.
     * Использует targetMin/targetMax из payload для гибкости.
     *
     * Зоны (по UIKit §8):
     *   criticalLow  < 3.0  — GlucoseDanger,  opacity 0.06
     *   low          3.0–4.0 — GlucoseWarning, opacity 0.05
     *   target       4.0–10.0 — GlucoseNormal, opacity 0.08
     *   high         10.0–14.0 — GlucoseWarning, opacity 0.06
     *   criticalHigh > 14.0 — GlucoseDanger,  opacity 0.06
     *
     * Пунктирные линии границ нормы — Primary, opacity 0.30.
     *
     * @param {{targetMin:number, targetMax:number, criticalLow:number, criticalHigh:number}} zones
     * @returns {object}
     */
    function buildAnnotations(zones) {
        if (!zones) return {};
        var tMin = zones.targetMin || GLUCOSE_LOW;
        var tMax = zones.targetMax || GLUCOSE_HIGH;
        var cLow = zones.criticalLow || GLUCOSE_CRITICAL_LOW;
        var cHigh = zones.criticalHigh || GLUCOSE_CRITICAL_HIGH;

        var normalColor = getGlucoseColor('normal');
        var warningColor = getGlucoseColor('warning');
        var dangerColor = getGlucoseColor('danger');
        var primaryColor = getGlucoseColor('primary');

        return {
            // Зона критически низкого (<criticalLow)
            zoneCriticalLow: {
                type: 'box',
                yMin: GLUCOSE_Y_MIN,
                yMax: cLow,
                backgroundColor: hexToRgba(dangerColor, 0.06),
                borderWidth: 0,
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Граница критически низкого (пунктир)
            lineCriticalLow: {
                type: 'line',
                yMin: cLow,
                yMax: cLow,
                borderColor: hexToRgba(dangerColor, 0.35),
                borderWidth: 1,
                borderDash: [5, 4],
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Зона низкого (criticalLow–targetMin)
            zoneLow: {
                type: 'box',
                yMin: cLow,
                yMax: tMin,
                backgroundColor: hexToRgba(warningColor, 0.05),
                borderWidth: 0,
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Целевая зона нормы (targetMin–targetMax) — GlucoseNormal 0.08
            zoneTarget: {
                type: 'box',
                yMin: tMin,
                yMax: tMax,
                backgroundColor: hexToRgba(normalColor, 0.08),
                borderWidth: 0,
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Нижняя граница нормы (пунктир Primary)
            lineTargetMin: {
                type: 'line',
                yMin: tMin,
                yMax: tMin,
                borderColor: hexToRgba(primaryColor, 0.30),
                borderWidth: 1,
                borderDash: [5, 4],
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Верхняя граница нормы (пунктир Primary)
            lineTargetMax: {
                type: 'line',
                yMin: tMax,
                yMax: tMax,
                borderColor: hexToRgba(primaryColor, 0.30),
                borderWidth: 1,
                borderDash: [5, 4],
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Зона высокого (targetMax–criticalHigh)
            zoneHigh: {
                type: 'box',
                yMin: tMax,
                yMax: cHigh,
                backgroundColor: hexToRgba(warningColor, 0.06),
                borderWidth: 0,
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            },
            // Зона критически высокого (>criticalHigh)
            zoneCriticalHigh: {
                type: 'box',
                yMin: cHigh,
                yMax: GLUCOSE_Y_MAX,
                backgroundColor: hexToRgba(dangerColor, 0.06),
                borderWidth: 0,
                drawTime: 'beforeDatasetsDraw',
                label: { display: false }
            }
        };
    }

    /**
     * Вычисляет массивы цветов точек по статусам.
     * @param {Array<'safe'|'warning'|'danger'>} colorKeys - цвет каждой точки
     * @param {number} alpha - прозрачность
     * @returns {string[]}
     */
    function buildPointColors(colorKeys, alpha) {
        if (!colorKeys || !colorKeys.length) return [];
        return colorKeys.map(function (k) {
            var stateMap = { safe: 'normal', warning: 'warning', danger: 'danger' };
            var state = stateMap[k] || 'normal';
            return hexToRgba(getGlucoseColor(state), alpha);
        });
    }

    /**
     * Chart.js повторно разрешает scriptable-опции после mouseout. В этой точке
     * dataIndex иногда отсутствует, из-за чего библиотека подставляла цвет линии
     * (зелёный) для любой точки. Храним готовые цвета на dataset и закрепляем их
     * на самих PointElement после каждого события.
     */
    function applyStablePointStyles(chart) {
        if (!chart || !chart.data || !chart.data.datasets || !chart.data.datasets.length) return;

        var dataset = chart.data.datasets[0];
        var pointColors = dataset._sgPointColors || [];
        var fallback = getGlucoseColor('normal');
        var points = chart.getDatasetMeta(0).data || [];

        points.forEach(function (point, index) {
            var color = pointColors[index] || fallback;
            point.options.backgroundColor = color;
            point.options.borderColor = color;
            point.options.borderWidth = 2;
            point.options.radius = 4;
            point.options.hoverRadius = 7;
        });
    }

    var stablePointStylePlugin = {
        id: 'sg-stable-point-styles',
        afterDatasetsUpdate: function (chart) {
            applyStablePointStyles(chart);
        },
        afterEvent: function (chart) {
            applyStablePointStyles(chart);
        }
    };

    /**
     * Строит полный конфигурационный объект Chart.js для графика глюкозы.
     * Используется в sgGlucoseChart.init / update.
     *
     * @param {HTMLCanvasElement} canvas
     * @param {{
     *   chartId: string,
     *   labels: string[],
     *   values: number[],
     *   colors: Array<'safe'|'warning'|'danger'>,
     *   zones: {targetMin:number, targetMax:number, criticalLow:number, criticalHigh:number},
     *   yMin: number,
     *   yMax: number,
     *   height: number
     * }} payload - данные из C# BuildChartPayload()
     * @returns {object} Chart.js config
     */
    function buildConfig(canvas, payload) {
        var ctx = canvas.getContext('2d');
        var normalColor = getGlucoseColor('normal');

        // Высота для градиента — реальная высота canvas или из payload
        var canvasH = canvas.offsetHeight || payload.height || 220;
        var gradient = ctx.createLinearGradient(0, 0, 0, canvasH);
        gradient.addColorStop(0, hexToRgba(normalColor, 0.28));
        gradient.addColorStop(1, hexToRgba(normalColor, 0.00));

        var pointColors = buildPointColors(payload.colors, 1.0);

        // Плагин аннотаций — подключаем только если CDN загрузил его
        var annotationPlugin = hasAnnotationPlugin()
            ? { annotations: buildAnnotations(payload.zones) }
            : {};

        return {
            type: 'line',
            data: {
                labels: payload.labels,
                datasets: [{
                    data: payload.values,
                    borderColor: normalColor,
                    borderWidth: 2,
                    fill: true,
                    backgroundColor: gradient,
                    tension: 0.4,   // spline-сглаживание
                    pointRadius: 4,
                    pointHoverRadius: 7,
                    pointBackgroundColor: pointColors,
                    pointBorderColor: pointColors,
                    pointBorderWidth: 2,
                    pointHoverBackgroundColor: pointColors,
                    pointHoverBorderColor: pointColors,
                    pointHoverBorderWidth: 2,
                    _sgPointColors: pointColors
                }]
            },
            plugins: [stablePointStylePlugin],
            options: {
                responsive: true,
                maintainAspectRatio: false,
                interaction: {
                    mode: 'nearest',
                    intersect: true
                },
                plugins: {
                    legend: { display: false },
                    tooltip: buildTooltipConfig(),
                    annotation: annotationPlugin
                },
                scales: {
                    x: buildXScale(false),
                    y: buildYScale(payload.yMin, payload.yMax, false)
                }
            }
        };
    }

    /* ================================================================
     * 5. API ОБЪЕКТА sgGlucoseChart
     * Используется GlucoseChart.razor через JS Interop:
     *   await JS.InvokeVoidAsync("sgGlucoseChart.init",  canvasRef, payload)
     *   await JS.InvokeVoidAsync("sgGlucoseChart.update", chartId,  payload)
     *   await JS.InvokeVoidAsync("sgGlucoseChart.destroy", chartId)
     * ================================================================ */

    var sgGlucoseChart = {

        /**
         * Инициализирует Chart.js на переданном canvas.
         * Blazor передаёт ElementReference — это может быть HTMLCanvasElement
         * напрямую, или объект с __internalId (зависит от версии Blazor).
         * Если canvas не найден по ElementReference — ищем по chartId.
         *
         * @param {HTMLCanvasElement|object} canvasElement - Blazor ElementReference
         * @param {object} payload - данные из C# BuildChartPayload()
         */
        init: function (canvasElement, payload) {
            // Blazor передаёт ElementReference — резолвим в реальный DOM-элемент
            var canvas = (canvasElement instanceof HTMLCanvasElement)
                ? canvasElement
                : document.getElementById(payload.chartId);

            if (!canvas) {
                console.warn('[sgGlucoseChart] canvas не найден, chartId:', payload.chartId);
                return;
            }

            // Уничтожаем предыдущий инстанс (защита от двойного init при hot-reload)
            destroyById(payload.chartId);

            try {
                var chart = new window.Chart(canvas, buildConfig(canvas, payload));
                instances.set(payload.chartId, chart);
            } catch (e) {
                console.error('[sgGlucoseChart] ошибка инициализации:', e);
            }
        },

        /**
         * Обновляет данные существующего графика без полного пересоздания.
         * Пересчитывает градиент и цвета точек по текущей теме.
         * Если инстанс не найден — выполняет полный init.
         *
         * @param {string} chartId
         * @param {object} payload
         */
        update: function (chartId, payload) {
            var chart = instances.get(chartId);
            if (!chart) {
                // Инстанс потерян (например, после Blazor-навигации)
                var canvas = getCanvas(chartId);
                if (canvas) {
                    sgGlucoseChart.init(canvas, payload);
                }
                return;
            }

            try {
                var ds = chart.data.datasets[0];
                chart.data.labels = payload.labels;
                ds.data = payload.values;

                // Пересчитываем градиент по актуальной теме
                var ctx = chart.ctx;
                var normalColor = getGlucoseColor('normal');
                var gradient = ctx.createLinearGradient(0, 0, 0, chart.height);
                gradient.addColorStop(0, hexToRgba(normalColor, 0.28));
                gradient.addColorStop(1, hexToRgba(normalColor, 0.00));

                ds.backgroundColor = gradient;
                ds.borderColor = normalColor;
                var pointColors = buildPointColors(payload.colors, 1.0);
                ds.pointBackgroundColor = pointColors;
                ds.pointBorderColor = pointColors;
                ds.pointBorderWidth = 2;
                ds.pointHoverBackgroundColor = pointColors;
                ds.pointHoverBorderColor = pointColors;
                ds.pointHoverBorderWidth = 2;
                ds.pointRadius = 4;
                ds.pointHoverRadius = 7;
                ds._sgPointColors = pointColors;

                // Обновляем аннотации если плагин доступен
                if (hasAnnotationPlugin() &&
                    chart.options.plugins &&
                    chart.options.plugins.annotation) {
                    chart.options.plugins.annotation.annotations =
                        buildAnnotations(payload.zones);
                }

                chart.update('none');
                applyStablePointStyles(chart);
            } catch (e) {
                console.error('[sgGlucoseChart] ошибка обновления:', e);
            }
        },

        /**
         * Уничтожает инстанцию Chart.js и освобождает canvas.
         * Вызывается из GlucoseChart.razor.DisposeAsync().
         *
         * @param {string} chartId
         */
        destroy: function (chartId) {
            destroyById(chartId);
        }
    };

    /* ================================================================
     * 6. API ОБЪЕКТА SugarGuardCharts
     * Используется Dashboard.razor, legacy-кодом и прямыми вызовами
     * через JS.InvokeVoidAsync("SugarGuardCharts.renderGlucose", ...).
     *
     * Все методы принимают JSON-строки (из C# JsonSerializer.Serialize)
     * и парсят их внутри через safeParse().
     * ================================================================ */

    var SugarGuardCharts = {

        /**
         * Рендерит линейный график глюкозы.
         *
         * @param {string}  canvasId
         * @param {string}  labelsJson   - JSON массив меток времени
         * @param {string}  valuesJson   - JSON массив значений (number[])
         * @param {string}  state        - 'normal' | 'warning' | 'danger'
         * @param {string|null} optsJson - дополнительные Chart.js опции (JSON)
         */
        renderGlucose: function (canvasId, labelsJson, valuesJson, state, optsJson) {
            var canvas = getCanvas(canvasId);
            if (!canvas || typeof window.Chart === 'undefined') return;

            var labels = safeParse(labelsJson) || [];
            var values = safeParse(valuesJson) || [];
            var opts = optsJson ? safeParse(optsJson) : null;

            destroyById(canvasId);

            var lineColor = getGlucoseColor(state || 'normal');
            var ctx = canvas.getContext('2d');
            var canvasH = canvas.offsetHeight || 220;
            var gradient = buildAreaGradient(ctx, canvasH, lineColor);

            var annotationPlugin = hasAnnotationPlugin()
                ? {
                    annotations: buildAnnotations({
                        targetMin: GLUCOSE_LOW,
                        targetMax: GLUCOSE_HIGH,
                        criticalLow: GLUCOSE_CRITICAL_LOW,
                        criticalHigh: GLUCOSE_CRITICAL_HIGH
                    })
                }
                : {};

            var config = {
                type: 'line',
                data: {
                    labels: labels,
                    datasets: [{
                        data: values,
                        borderColor: lineColor,
                        borderWidth: 2,
                        fill: true,
                        backgroundColor: gradient,
                        tension: 0.4,
                        pointRadius: 0,
                        pointHoverRadius: 5
                    }]
                },
                options: Object.assign({
                    responsive: true,
                    maintainAspectRatio: false,
                    interaction: { mode: 'index', intersect: false },
                    plugins: {
                        legend: { display: false },
                        tooltip: buildTooltipConfig(),
                        annotation: annotationPlugin
                    },
                    scales: {
                        x: buildXScale(false),
                        y: buildYScale(GLUCOSE_Y_MIN, GLUCOSE_Y_MAX, false)
                    }
                }, opts || {})
            };

            instances.set(canvasId, new window.Chart(canvas, config));
        },

        /**
         * Обновляет данные в существующем линейном графике глюкозы.
         * При отсутствии инстанса — создаёт новый.
         *
         * @param {string} canvasId
         * @param {string} labelsJson
         * @param {string} valuesJson
         * @param {string} state - 'normal' | 'warning' | 'danger'
         */
        updateGlucose: function (canvasId, labelsJson, valuesJson, state) {
            var chart = instances.get(canvasId);
            if (!chart) {
                SugarGuardCharts.renderGlucose(canvasId, labelsJson, valuesJson, state, null);
                return;
            }
            try {
                var labels = safeParse(labelsJson) || [];
                var values = safeParse(valuesJson) || [];
                var newColor = getGlucoseColor(state || 'normal');
                var ctx = chart.ctx;
                var gradient = buildAreaGradient(ctx, chart.height, newColor);

                chart.data.labels = labels;
                chart.data.datasets[0].data = values;
                chart.data.datasets[0].borderColor = newColor;
                chart.data.datasets[0].backgroundColor = gradient;
                chart.update('active');
            } catch (e) {
                console.error('[SugarGuardCharts] updateGlucose error:', e);
            }
        },

        /**
         * Рендерит TIR-диаграмму (дoughnut).
         * В центре — ClashDisplay Bold, процент TIR.
         *
         * @param {string} canvasId
         * @param {number} inRange  - % в норме
         * @param {number} high     - % выше нормы
         * @param {number} low      - % ниже нормы
         */
        renderTir: function (canvasId, inRange, high, low) {
            var canvas = getCanvas(canvasId);
            if (!canvas || typeof window.Chart === 'undefined') return;

            destroyById(canvasId);

            var total = Number(inRange) + Number(high) + Number(low);
            if (total === 0) { inRange = 100; high = 0; low = 0; total = 100; }

            var normIn = Math.round(Number(inRange) / total * 100);
            var normHigh = Math.round(Number(high) / total * 100);
            var normLow = 100 - normIn - normHigh;

            var normalColor = getGlucoseColor('normal');
            var warningColor = getGlucoseColor('warning');
            var dangerColor = getGlucoseColor('danger');

            // Кастомный плагин: рисует процент TIR в центре дonuta
            var fontDisplay = cssVar('--font-display', 'ClashDisplay, Satoshi, sans-serif');
            var fontBody = cssVar('--font-body', 'Satoshi, system-ui, sans-serif');

            var centerLabelPlugin = {
                id: 'sg-tir-center-' + canvasId,
                afterDraw: function (chart) {
                    var ctx2 = chart.ctx;
                    var cx = chart.width / 2;
                    var cy = chart.height / 2;
                    var textColor = cssVar('--color-text', '#16213E');
                    var mutedColor = cssVar('--color-text-muted', '#667694');

                    ctx2.save();
                    ctx2.textAlign = 'center';
                    ctx2.textBaseline = 'middle';

                    // Крупное значение — ClashDisplay Bold
                    ctx2.font = '700 28px ' + fontDisplay;
                    ctx2.fillStyle = textColor;
                    ctx2.fillText(normIn + '%', cx, cy - 10);

                    // Подпись "TIR" — Satoshi
                    ctx2.font = '400 12px ' + fontBody;
                    ctx2.fillStyle = mutedColor;
                    ctx2.fillText('TIR', cx, cy + 14);

                    ctx2.restore();
                }
            };

            var config = {
                type: 'doughnut',
                data: {
                    labels: ['Норма', 'Высокий', 'Низкий'],
                    datasets: [{
                        data: [normIn, normHigh, normLow],
                        backgroundColor: [normalColor, warningColor, dangerColor],
                        borderWidth: 0,
                        hoverOffset: 4
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    cutout: '72%',
                    plugins: {
                        legend: { display: false },
                        tooltip: buildTooltipConfig()
                    }
                },
                plugins: [centerLabelPlugin]
            };

            instances.set(canvasId, new window.Chart(canvas, config));
        },

        /**
         * Обновляет TIR-диаграмму.
         *
         * @param {string} canvasId
         * @param {number} inRange
         * @param {number} high
         * @param {number} low
         */
        updateTir: function (canvasId, inRange, high, low) {
            var chart = instances.get(canvasId);
            if (!chart) {
                SugarGuardCharts.renderTir(canvasId, inRange, high, low);
                return;
            }
            try {
                var total = Number(inRange) + Number(high) + Number(low);
                if (total === 0) { inRange = 100; high = 0; low = 0; total = 100; }
                var normIn = Math.round(Number(inRange) / total * 100);
                var normHigh = Math.round(Number(high) / total * 100);
                var normLow = 100 - normIn - normHigh;
                chart.data.datasets[0].data = [normIn, normHigh, normLow];
                chart.update('active');
            } catch (e) {
                console.error('[SugarGuardCharts] updateTir error:', e);
            }
        },

        /**
         * Рендерит мини-спарклайн для KPI-карточки.
         * Без осей, без тултипов, минимальный вес.
         *
         * @param {string} canvasId
         * @param {string} valuesJson   - JSON number[]
         * @param {string} state        - 'normal' | 'warning' | 'danger' | 'primary'
         */
        renderSparkline: function (canvasId, valuesJson, state) {
            var canvas = getCanvas(canvasId);
            if (!canvas || typeof window.Chart === 'undefined') return;

            destroyById(canvasId);

            var values = safeParse(valuesJson) || [];
            var resolvedState = (state && state !== 'primary') ? state : null;
            var lineColor = resolvedState
                ? getGlucoseColor(resolvedState)
                : cssVar('--color-primary', FALLBACK_COLORS.primary.light);
            var ctx = canvas.getContext('2d');
            var canvasH = canvas.offsetHeight || 40;
            var gradient = buildAreaGradient(ctx, canvasH, lineColor);

            var config = {
                type: 'line',
                data: {
                    labels: values.map(function (_, i) { return i; }),
                    datasets: [{
                        data: values,
                        borderColor: lineColor,
                        borderWidth: 1.5,
                        fill: true,
                        backgroundColor: gradient,
                        tension: 0.4,
                        pointRadius: 0,
                        pointHoverRadius: 0
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    animation: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: { enabled: false }
                    },
                    scales: {
                        x: buildXScale(true),
                        y: buildYScale(undefined, undefined, true)
                    }
                }
            };

            instances.set(canvasId, new window.Chart(canvas, config));
        },

        /**
         * Обновляет спарклайн.
         *
         * @param {string} canvasId
         * @param {string} valuesJson
         * @param {string} state
         */
        updateSparkline: function (canvasId, valuesJson, state) {
            var chart = instances.get(canvasId);
            if (!chart) {
                SugarGuardCharts.renderSparkline(canvasId, valuesJson, state);
                return;
            }
            try {
                var values = safeParse(valuesJson) || [];
                var resolvedState = (state && state !== 'primary') ? state : null;
                var newColor = resolvedState
                    ? getGlucoseColor(resolvedState)
                    : cssVar('--color-primary', FALLBACK_COLORS.primary.light);
                var ctx = chart.ctx;
                var gradient = buildAreaGradient(ctx, chart.height, newColor);

                chart.data.labels = values.map(function (_, i) { return i; });
                chart.data.datasets[0].data = values;
                chart.data.datasets[0].borderColor = newColor;
                chart.data.datasets[0].backgroundColor = gradient;
                chart.update('active');
            } catch (e) {
                console.error('[SugarGuardCharts] updateSparkline error:', e);
            }
        },

        /**
         * Рендерит почасовую bar-диаграмму (для страницы статистики).
         *
         * @param {string} canvasId
         * @param {string} labelsJson  - JSON string[] (часы '00'..'23')
         * @param {string} valuesJson  - JSON number[]
         */
        renderHourlyBar: function (canvasId, labelsJson, valuesJson) {
            var canvas = getCanvas(canvasId);
            if (!canvas || typeof window.Chart === 'undefined') return;

            destroyById(canvasId);

            var labels = safeParse(labelsJson) || [];
            var values = safeParse(valuesJson) || [];
            var barColor = getGlucoseColor('normal');

            var config = {
                type: 'bar',
                data: {
                    labels: labels,
                    datasets: [{
                        data: values,
                        backgroundColor: hexToRgba(barColor, 0.65),
                        borderColor: hexToRgba(barColor, 1.0),
                        borderWidth: 1,
                        borderRadius: 4,
                        borderSkipped: false
                    }]
                },
                options: {
                    responsive: true,
                    maintainAspectRatio: false,
                    plugins: {
                        legend: { display: false },
                        tooltip: buildTooltipConfig()
                    },
                    scales: {
                        x: buildXScale(false),
                        y: buildYScale(GLUCOSE_Y_MIN, GLUCOSE_Y_MAX, false)
                    }
                }
            };

            instances.set(canvasId, new window.Chart(canvas, config));
        },

        /**
         * Обновляет почасовую bar-диаграмму.
         *
         * @param {string} canvasId
         * @param {string} labelsJson
         * @param {string} valuesJson
         */
        updateHourlyBar: function (canvasId, labelsJson, valuesJson) {
            var chart = instances.get(canvasId);
            if (!chart) {
                SugarGuardCharts.renderHourlyBar(canvasId, labelsJson, valuesJson);
                return;
            }
            try {
                chart.data.labels = safeParse(labelsJson) || [];
                chart.data.datasets[0].data = safeParse(valuesJson) || [];
                chart.update('active');
            } catch (e) {
                console.error('[SugarGuardCharts] updateHourlyBar error:', e);
            }
        },

        /**
         * Уничтожает инстанцию графика по canvasId.
         *
         * @param {string} canvasId
         */
        destroy: function (canvasId) {
            destroyById(canvasId);
        },

        /**
         * Уничтожает ВСЕ инстанции.
         * Вызывается при Blazor-навигации (IAsyncDisposable).
         */
        destroyAll: function () {
            instances.forEach(function (chart) {
                try { chart.destroy(); } catch (e) { /* canvas уже вне DOM */ }
            });
            instances.clear();
        },

        /**
         * Перерисовывает цвета тултипов и сеток при смене темы.
         * Вызывается из theme.js после установки data-theme.
         * Таймаут 50ms — ждём применения CSS-переменных браузером.
         */
        applyTheme: function () {
            if (typeof window.Chart === 'undefined') return;
            instances.forEach(function (chart) {
                try {
                    // Обновляем тултип
                    if (chart.options.plugins && chart.options.plugins.tooltip) {
                        Object.assign(
                            chart.options.plugins.tooltip,
                            buildTooltipConfig()
                        );
                    }
                    // Обновляем цвета осей (если есть)
                    if (chart.options.scales) {
                        if (chart.options.scales.x) {
                            var xOpts = buildXScale(false);
                            Object.assign(chart.options.scales.x.grid, xOpts.grid);
                            Object.assign(chart.options.scales.x.ticks, xOpts.ticks);
                        }
                        if (chart.options.scales.y) {
                            var yOpts = buildYScale(undefined, undefined, false);
                            Object.assign(chart.options.scales.y.grid, yOpts.grid);
                            Object.assign(chart.options.scales.y.ticks, yOpts.ticks);
                        }
                    }
                    // Обновляем аннотации глюкозных зон
                    if (hasAnnotationPlugin() &&
                        chart.options.plugins &&
                        chart.options.plugins.annotation) {
                        chart.options.plugins.annotation.annotations =
                            buildAnnotations({
                                targetMin: GLUCOSE_LOW,
                                targetMax: GLUCOSE_HIGH,
                                criticalLow: GLUCOSE_CRITICAL_LOW,
                                criticalHigh: GLUCOSE_CRITICAL_HIGH
                            });
                    }
                    chart.update('none');
                } catch (e) {
                    // chart уже уничтожен
                }
            });
        },

        /**
         * Возвращает массив ID всех активных графиков.
         * @returns {string[]}
         */
        getRegisteredIds: function () {
            return Array.from(instances.keys());
        },

        /**
         * Проверяет, зарегистрирован ли график по canvasId.
         * @param {string} canvasId
         * @returns {boolean}
         */
        isRegistered: function (canvasId) {
            return instances.has(canvasId);
        }
    };

    /* ================================================================
     * 7. ПОДПИСКА НА СМЕНУ ТЕМЫ
     * theme.js вызывает window.SugarGuardCharts.applyTheme() сам,
     * но если theme.js загружен позже — слушаем MutationObserver.
     * ================================================================ */

    function subscribeToThemeChanges() {
        var observer = new MutationObserver(function (mutations) {
            mutations.forEach(function (m) {
                if (m.attributeName === 'data-theme') {
                    // Небольшая задержка — ждём применения CSS-переменных
                    setTimeout(function () {
                        SugarGuardCharts.applyTheme();
                    }, 50);
                }
            });
        });
        observer.observe(document.documentElement, { attributes: true });
    }

    function init() {
        subscribeToThemeChanges();
    }

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    /* ================================================================
     * 8. ЭКСПОРТ В ГЛОБАЛЬНЫЙ SCOPE
     * Два объекта для обратной совместимости.
     * ================================================================ */

    // window.sgGlucoseChart — для GlucoseChart.razor (JSInterop)
    window.sgGlucoseChart = sgGlucoseChart;

    // window.SugarGuardCharts — для Dashboard.razor и legacy-кода
    if (typeof window.SugarGuardCharts !== 'undefined') {
        // Мёрджим, чтобы не сломать уже подписанные обработчики
        Object.assign(window.SugarGuardCharts, SugarGuardCharts);
    } else {
        window.SugarGuardCharts = SugarGuardCharts;
    }

    // Обратная совместимость: window.SugarGuard (старый Dashboard.razor)
    window.SugarGuard = window.SugarGuard || {};
    window.SugarGuard.renderGlucoseChart = function (canvasId, labelsJson, valuesJson, colorHex) {
        // colorHex игнорируем — используем CSS-переменные
        SugarGuardCharts.renderGlucose(canvasId, labelsJson, valuesJson, 'normal', null);
    };

}(window));

/**
 * Регистрирует Service Worker и подписывает браузер на Web Push.
 * @param {string} vapidPublicKey — публичный VAPID-ключ из appsettings.
 * @param {string} apiBaseUrl — базовый URL API (например, https://localhost:7247).
 * @param {string} bearerToken — JWT-токен текущего пользователя.
 */
window.registerPushSubscription = async function (vapidPublicKey, apiBaseUrl, bearerToken) {
    if (!('serviceWorker' in navigator) || !('PushManager' in window)) {
        console.warn('Web Push не поддерживается браузером.');
        return;
    }

    try {
        const registration = await navigator.serviceWorker.register('/js/service-worker.js');
        await navigator.serviceWorker.ready;

        const permission = await Notification.requestPermission();
        if (permission !== 'granted') {
            console.info('Пользователь отклонил Push-уведомления.');
            return;
        }

        // Конвертация VAPID public key из Base64url в Uint8Array
        const applicationServerKey = _urlBase64ToUint8Array(vapidPublicKey);

        const subscription = await registration.pushManager.subscribe({
            userVisibleOnly: true,
            applicationServerKey
        });

        const subJson = subscription.toJSON();

        await fetch(`${apiBaseUrl}/api/push/subscribe`, {
            method: 'POST',
            headers: {
                'Content-Type': 'application/json',
                'Authorization': `Bearer ${bearerToken}`
            },
            body: JSON.stringify({
                endpoint: subJson.endpoint,
                p256dh: subJson.keys.p256dh,
                auth: subJson.keys.auth,
                userAgent: navigator.userAgent
            })
        });

        console.info('Push-подписка зарегистрирована.');
    } catch (err) {
        console.error('Ошибка регистрации Push-подписки:', err);
    }
};

/** Base64url → Uint8Array (нужно для applicationServerKey) */
function _urlBase64ToUint8Array(base64String) {
    const padding = '='.repeat((4 - base64String.length % 4) % 4);
    const base64 = (base64String + padding).replace(/-/g, '+').replace(/_/g, '/');
    const rawData = window.atob(base64);
    return Uint8Array.from([...rawData].map(c => c.charCodeAt(0)));
}
