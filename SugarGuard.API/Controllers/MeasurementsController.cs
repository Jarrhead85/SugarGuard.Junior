using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Application.Glucose;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управление измерениями глюкозы
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/measurements")]
[Produces("application/json")]
public class MeasurementsController : ControllerBase
{
    private readonly IMeasurementsService _measurements;
    private readonly IStatisticsCalculationService _statisticsCalculation;
    private readonly IGlucoseStatusService _glucoseStatusService;
    private readonly IGlucoseUiStateService _glucoseUiStateService;
    private readonly IChildAccessService _childAccess;
    private readonly ITelegramNotificationService _notificationService;
    private readonly IPdfExportService _pdfExportService;
    private readonly ILogger<MeasurementsController> _logger;

    /// <summary>
    /// Инициализирует контроллер измерений.
    /// </summary>
    public MeasurementsController(
        IMeasurementsService measurements,
        IStatisticsCalculationService statisticsCalculation,
        IGlucoseStatusService glucoseStatusService,
        IGlucoseUiStateService glucoseUiStateService,
        IChildAccessService childAccess,
        ITelegramNotificationService notificationService,
        IPdfExportService pdfExportService,
        ILogger<MeasurementsController> logger)
    {
        _measurements = measurements;
        _statisticsCalculation = statisticsCalculation;
        _glucoseStatusService = glucoseStatusService;
        _glucoseUiStateService = glucoseUiStateService;
        _childAccess = childAccess;
        _notificationService = notificationService;
        _pdfExportService = pdfExportService;
        _logger = logger;
    }

    // POST api/measurements
    /// <summary>
    /// Создаёт новое измерение глюкозы
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(MeasurementResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MeasurementResponse>> CreateMeasurement(
        [FromBody] CreateMeasurementRequest request,
        CancellationToken cancellationToken)
    {
        if (!await _measurements.ChildExistsAsync(request.ChildId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
            return Forbid();

        Domain.Entities.Measurement measurement;
        try
        {
            measurement = await _measurements.CreateAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CreateMeasurement: внутренняя ошибка. ChildId={ChildId}.", request.ChildId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }

        var glucoseStatus = _glucoseStatusService.GetGlucoseStatus(request.GlucoseValue);
        var isCritical = _glucoseStatusService.IsCritical(request.GlucoseValue);
        var response = measurement.ToResponse(_glucoseStatusService, _glucoseUiStateService);

        _logger.LogInformation(
            "CreateMeasurement: MeasurementId={MeasurementId} ChildId={ChildId} " +
            "Glucose={GlucoseValue} Status={Status}.",
            measurement.MeasurementId, measurement.ChildId,
            measurement.GlucoseValue, glucoseStatus);

        await SendMeasurementNotificationsAsync(measurement, glucoseStatus, isCritical);

        return CreatedAtAction(
            nameof(GetMeasurement),
            new { id = measurement.MeasurementId },
            response);
    }

    /// <summary>
    /// Отправляет уведомления о новом измерении
    /// </summary>
    private async Task SendMeasurementNotificationsAsync(
        Domain.Entities.Measurement measurement,
        string glucoseStatus,
        bool isCritical)
    {
        try
        {
            var notificationResult = await _notificationService.SendMeasurementNotificationAsync(
                new MeasurementNotificationRequest
                {
                    ChildId = measurement.ChildId.ToString(),
                    GlucoseValue = (double)measurement.GlucoseValue,
                    Status = glucoseStatus,
                    MeasurementTime = measurement.MeasurementTime,
                    Notes = measurement.Notes
                });

            if (notificationResult.Success)
            {
                _logger.LogInformation(
                    "CreateMeasurement: уведомление отправлено {Count} родителям.",
                    notificationResult.ParentsNotified);
            }
            else
            {
                _logger.LogWarning(
                    "CreateMeasurement: уведомление не отправлено. Error={Error}.",
                    notificationResult.ErrorMessage);
            }

            if (isCritical)
            {
                var criticalResult = await _notificationService.SendCriticalAlertAsync(
                    new CriticalAlertRequest
                    {
                        ChildId = measurement.ChildId.ToString(),
                        CriticalGlucose = (double)measurement.GlucoseValue,
                        MeasurementTime = measurement.MeasurementTime
                    });

                if (criticalResult.Success)
                {
                    _logger.LogWarning(
                        "CreateMeasurement: критический алерт отправлен {Count} родителям.",
                        criticalResult.ParentsNotified);
                }
                else
                {
                    _logger.LogError(
                        "CreateMeasurement: критический алерт НЕ отправлен. Error={Error}.",
                        criticalResult.ErrorMessage);
                }
            }
        }
        catch (Exception notificationEx)
        {
            _logger.LogError(notificationEx,
                "CreateMeasurement: ошибка отправки уведомления. MeasurementId={MeasurementId}.",
                measurement.MeasurementId);
        }
    }

    // GET api/measurements/{childId}
    /// <summary>
    /// Возвращает список измерений ребёнка за указанный период
    /// </summary>
    [HttpGet("{childId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<MeasurementResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<MeasurementResponse>>> GetMeasurements(
        Guid childId,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        if (!await _measurements.ChildExistsAsync(childId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var measurements = await _measurements.GetByChildAsync(
                childId, from, to, limit, cancellationToken);

            var response = measurements
                .Select(m => m.ToResponse(_glucoseStatusService, _glucoseUiStateService))
                .ToList();

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetMeasurements: внутренняя ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // GET api/measurements/{childId}/latest
    /// <summary>
    /// Возвращает последнее измерение ребёнка
    /// </summary>
    [HttpGet("{childId:guid}/latest")]
    [ProducesResponseType(typeof(MeasurementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MeasurementResponse>> GetLatestMeasurement(
        Guid childId,
        CancellationToken cancellationToken)
    {
        if (!await _measurements.ChildExistsAsync(childId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var measurement = await _measurements.GetLatestAsync(childId, cancellationToken);

            if (measurement is null)
            {
                return this.ProblemWithCode(404, "No Measurements",
                    "Измерения не найдены", "no_measurements");
            }

            return Ok(measurement.ToResponse(_glucoseStatusService, _glucoseUiStateService));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetLatestMeasurement: внутренняя ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // GET api/measurements/by-id/{id}
    /// <summary>
    /// Возвращает измерение по его ID
    /// </summary>
    [HttpGet("by-id/{id:guid}")]
    [ProducesResponseType(typeof(MeasurementResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<MeasurementResponse>> GetMeasurement(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var measurement = await _measurements.GetByIdAsync(id, cancellationToken);

            if (measurement is null)
            {
                return this.ProblemWithCode(404, "Measurement Not Found",
                    "Измерение не найдено", "measurement_not_found");
            }

            // IDOR-защита: проверяем доступ к ребёнку ПОСЛЕ загрузки записи
            if (!await _childAccess.CanAccessChildAsync(measurement.ChildId, cancellationToken))
                return Forbid();

            return Ok(measurement.ToResponse(_glucoseStatusService, _glucoseUiStateService));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetMeasurement: внутренняя ошибка. MeasurementId={MeasurementId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // GET api/measurements/{childId}/statistics
    /// <summary>
    /// Возвращает статистику измерений ребёнка за указанный период
    /// </summary>
    [HttpGet("{childId:guid}/statistics")]
    [ProducesResponseType(typeof(StatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<StatisticsResponse>> GetStatistics(
    Guid childId,
    [FromQuery] string period = "day",
    [FromQuery] DateTime? date = null,
    CancellationToken cancellationToken = default)
    {
        if (!await _measurements.ChildExistsAsync(childId, cancellationToken))
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var (fromDate, toDate, periodName) = _statisticsCalculation.GetPeriodRange(period, targetDate);

            var measurements = await _measurements.GetForStatisticsAsync(
                childId, fromDate, toDate, cancellationToken);

            var statistics = _statisticsCalculation.CalculateStatistics(measurements.ToList());

            var response = new StatisticsResponse
            {
                ChildId = childId,
                Period = periodName,
                FromDate = fromDate,
                ToDate = toDate,
                TotalMeasurements = statistics.TotalMeasurements,
                AverageGlucose = statistics.AverageGlucose,
                MinGlucose = statistics.MinGlucose,
                MaxGlucose = statistics.MaxGlucose,
                StandardDeviation = statistics.StandardDeviation,
                TimeInTargetRange = statistics.TimeInTargetRange,
                HypoEpisodes = statistics.HypoEpisodes,
                HyperEpisodes = statistics.HyperEpisodes,
                CriticalEpisodes = statistics.CriticalEpisodes,
                Measurements = measurements
                    .Select(m => m.ToResponse(_glucoseStatusService, _glucoseUiStateService))
                    .ToList()
            };

            _logger.LogInformation(
                "GetStatistics: ChildId={ChildId} Period={Period} Count={Count}.",
                childId, periodName, measurements.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetStatistics: внутренняя ошибка. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }

    // POST api/measurements/{childId}/export-pdf
    /// <summary>
    /// Генерирует PDF-отчёт по измерениям ребёнка за указанный период
    /// </summary>
    [HttpPost("{childId:guid}/export-pdf")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> ExportStatisticsToPdf(
        Guid childId,
        [FromQuery] string period = "day",
        [FromQuery] DateTime? date = null,
        [FromQuery] bool detailed = false,
        CancellationToken cancellationToken = default)
    {
        var child = await _measurements.GetChildAsync(childId, cancellationToken);

        if (child is null)
        {
            return this.ProblemWithCode(404, "Child Not Found",
                "Ребёнок не найден", "child_not_found");
        }

        if (!await _childAccess.CanAccessChildAsync(childId, cancellationToken))
            return Forbid();

        try
        {
            var targetDate = date ?? DateTime.UtcNow;
            var (fromDate, toDate, periodName) = _statisticsCalculation.GetPeriodRange(period, targetDate);

            var measurements = await _measurements.GetForStatisticsAsync(
                childId, fromDate, toDate, cancellationToken);

            var statistics = _statisticsCalculation.CalculateStatistics(measurements.ToList());

            var statisticsResponse = new StatisticsResponse
            {
                ChildId = childId,
                Period = periodName,
                FromDate = fromDate,
                ToDate = toDate,
                TotalMeasurements = statistics.TotalMeasurements,
                AverageGlucose = statistics.AverageGlucose,
                MinGlucose = statistics.MinGlucose,
                MaxGlucose = statistics.MaxGlucose,
                StandardDeviation = statistics.StandardDeviation,
                TimeInTargetRange = statistics.TimeInTargetRange,
                HypoEpisodes = statistics.HypoEpisodes,
                HyperEpisodes = statistics.HyperEpisodes,
                CriticalEpisodes = statistics.CriticalEpisodes,
                Measurements = measurements
                    .Select(m => m.ToResponse(_glucoseStatusService, _glucoseUiStateService))
                    .ToList()
            };

            var childName = $"{child.FirstName} {child.LastName}".Trim();
            if (string.IsNullOrEmpty(childName))
            {
                childName = "Ребёнок";
            }

            byte[] pdfBytes;
            string fileName;

            if (detailed)
            {
                pdfBytes = await _pdfExportService.GenerateDetailedReportAsync(statisticsResponse, childName);
                fileName = $"SugarGuard_Detailed_{childName}_{periodName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            }
            else
            {
                pdfBytes = await _pdfExportService.GenerateStatisticsReportAsync(statisticsResponse, childName);
                fileName = $"SugarGuard_Report_{childName}_{periodName}_{DateTime.UtcNow:yyyyMMdd}.pdf";
            }

            _logger.LogInformation(
                "ExportStatisticsToPdf: ChildId={ChildId} Period={Period} " +
                "Detailed={Detailed} Size={Size} bytes.",
                childId, periodName, detailed, pdfBytes.Length);

            return File(pdfBytes, "application/pdf", fileName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ExportStatisticsToPdf: ошибка генерации PDF. ChildId={ChildId}.", childId);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "pdf_generation_error", message = "Ошибка при генерации PDF." });
        }
    }

    // POST api/measurements/sync
    /// <summary>
    /// Синхронизация измерений от MAUI-приложения
    /// </summary>
    [HttpPost("sync")]
    [ProducesResponseType(typeof(SyncMeasurementsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SyncMeasurementsResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<SyncMeasurementsResponse>> SyncMeasurements(
        [FromBody] SyncMeasurementsRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Measurements.Count == 0)
        {
            return BadRequest(new SyncMeasurementsResponse
            {
                Success = false,
                ErrorMessage = "No measurements to sync"
            });
        }

        if (request.Measurements.Count > 1000)
        {
            return BadRequest(new SyncMeasurementsResponse
            {
                Success = false,
                ErrorMessage = "Too many measurements in one sync batch (max 1000)"
            });
        }

        try
        {
            var result = await _measurements.SyncBatchAsync(
                request,
                getAccessibleChildIdsAsync: ct => _childAccess.GetAccessibleChildIdsAsync(ct),
                cancellationToken);

            return Ok(new SyncMeasurementsResponse
            {
                Success = result.ErrorCount == 0,
                SyncedCount = result.SuccessCount,
                SuccessCount = result.SuccessCount,
                ErrorCount = result.ErrorCount,
                ErrorMessage = result.ErrorCount == 0
                    ? null
                    : "Some measurements failed or conflicted",
                Conflicts = result.Conflicts.ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SyncMeasurements: внутренняя ошибка.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Внутренняя ошибка сервера." });
        }
    }
}
