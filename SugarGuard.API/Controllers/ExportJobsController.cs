using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Services;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Управление задачами экспорта данных в CSV
/// </summary>
[Authorize(Policy = "ParentOrDoctorOrAdmin")]
[ApiController]
[Route("api/export-jobs")]
[Produces("application/json")]
public class ExportJobsController : ControllerBase
{
    /// <summary>
    /// Шаблон имени файла экспорта на диске
    /// </summary>
    private const string ExportFileNameFormat = "export-{0:N}.csv";

    private readonly IExportJobApiService _exportApi;
    private readonly IChildAccessService _childAccess;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<ExportJobsController> _logger;

    public ExportJobsController(
        IExportJobApiService exportApi,
        IChildAccessService childAccess,
        IWebHostEnvironment environment,
        ILogger<ExportJobsController> logger)
    {
        _exportApi = exportApi;
        _childAccess = childAccess;
        _environment = environment;
        _logger = logger;
    }

    // POST api/export-jobs
    /// <summary>
    /// Создаёт задачу экспорта измерений в CSV и ставит её в фоновую очередь
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(ExportJobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExportJobResponse>> Create(
        [FromBody] CreateExportJobRequest request,
        CancellationToken cancellationToken)
    {
        var currentUserId = _childAccess.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return this.ProblemWithCode(401, "Unauthorized",
                "Пользователь не авторизован", "unauthorized");
        }

        if (request.PeriodFrom > request.PeriodTo)
        {
            return this.ProblemWithCode(400, "Invalid Period",
                "PeriodFrom must be less than PeriodTo", "invalid_period");
        }

        var format = (request.Format ?? "csv").Trim().ToLowerInvariant();
        if (format != "csv")
        {
            return this.ProblemWithCode(400, "Unsupported Format",
                "Only csv is supported for MVP", "unsupported_format");
        }

        if (!await _childAccess.CanAccessChildAsync(request.ChildId, cancellationToken))
            return Forbid();

        try
        {
            var job = await _exportApi.CreateAsync(request, currentUserId.Value, cancellationToken);

            return CreatedAtAction(
                actionName: nameof(Download),
                routeValues: new { id = job.ExportJobId },
                value: ToResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Create: ошибка создания задачи экспорта. ChildId={ChildId}.",
                request.ChildId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "enqueue_failed", message = "Не удалось поставить задачу экспорта в очередь." });
        }
    }

    // GET api/export-jobs
    /// <summary>
    /// Возвращает список задач экспорта текущего пользователя
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ExportJobResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<IReadOnlyList<ExportJobResponse>>> GetList(
        [FromQuery] Guid? childId,
        [FromQuery] int limit = 100,
        CancellationToken cancellationToken = default)
    {
        var currentUserId = _childAccess.GetCurrentUserId();
        if (!currentUserId.HasValue)
        {
            return this.ProblemWithCode(401, "Unauthorized",
                "Пользователь не авторизован", "unauthorized");
        }

        try
        {
            if (childId.HasValue
                && !await _childAccess.CanAccessChildAsync(childId.Value, cancellationToken))
            {
                return Forbid();
            }

            var jobs = await _exportApi.GetListAsync(
                childId, currentUserId.Value, limit, cancellationToken);
            return Ok(jobs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "GetList: ошибка. UserId={UserId} ChildId={ChildId}.",
                currentUserId.Value, childId);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить список экспортов." });
        }
    }

    // GET api/export-jobs/{id}/status
    /// <summary>
    /// Возвращает текущий статус задачи экспорта
    /// </summary>
    [HttpGet("{id:guid}/status")]
    [ProducesResponseType(typeof(ExportJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<ExportJobResponse>> GetStatus(
        Guid id,
        CancellationToken cancellationToken)
    {
        try
        {
            var job = await _exportApi.GetByIdAsync(id, cancellationToken);
            if (job is null)
            {
                return this.ProblemWithCode(404, "Export Job Not Found",
                    "Export job not found", "job_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(job.ChildId, cancellationToken))
                return Forbid();

            return Ok(ToResponse(job));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetStatus: ошибка. ExportJobId={ExportJobId}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось получить статус задачи." });
        }
    }

    // GET api/export-jobs/{id}/download
    /// <summary>
    /// Скачивает CSV-файл экспорта по ID задачи
    /// </summary>
    [HttpGet("{id:guid}/download")]
    [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var job = await _exportApi.GetByIdAsync(id, cancellationToken);
            if (job is null)
            {
                return this.ProblemWithCode(404, "Export Job Not Found",
                    "Export job not found", "job_not_found");
            }

            if (!await _childAccess.CanAccessChildAsync(job.ChildId, cancellationToken))
                return Forbid();

            var exportsDirectory = Path.Combine(_environment.ContentRootPath, "exports");
            var fileName = string.Format(ExportFileNameFormat, job.ExportJobId);
            var filePath = Path.GetFullPath(Path.Combine(exportsDirectory, fileName));

            if (!System.IO.File.Exists(filePath))
            {
                return this.ProblemWithCode(404, "Export File Not Found",
                    "Export file not found on disk", "file_not_found");
            }

            await _exportApi.RecordDownloadedAsync(id, cancellationToken);

            _logger.LogInformation(
                "Download: файл скачан. ExportJobId={ExportJobId} ChildId={ChildId}.",
                job.ExportJobId, job.ChildId);

            return PhysicalFile(filePath, "text/csv",
                fileDownloadName: $"sugarguard_export_{job.ExportJobId:N}.csv",
                enableRangeProcessing: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Download: ошибка при скачивании файла. ExportJobId={ExportJobId}.", id);

            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось скачать файл экспорта." });
        }
    }

    // DTO mapping
    private static ExportJobResponse ToResponse(ExportJob job) => new()
    {
        ExportJobId = job.ExportJobId,
        RequestedByUserId = job.RequestedByUserId,
        ChildId = job.ChildId,
        PeriodFrom = job.PeriodFrom,
        PeriodTo = job.PeriodTo,
        Format = job.Format,
        Status = job.Status,
        DownloadUrl = job.DownloadUrl,
        CreatedAt = job.CreatedAt,
        CompletedAt = job.CompletedAt
    };
}
