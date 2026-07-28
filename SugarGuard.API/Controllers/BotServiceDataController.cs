using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;
using SugarGuard.API.Filters;
using SugarGuard.Application.Glucose;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Ограниченные операции Telegram-бота с данными ребёнка.
/// Доступ проверяется по Telegram ID и только для детей, связанных с владельцем бота.
/// </summary>
[BotServiceApiKey]
[AllowAnonymous]
[ApiController]
[Route("api/bot-service/data")]
[Produces("application/json")]
public sealed class BotServiceDataController : ControllerBase
{
    private readonly IBotUserContextService _botContext;
    private readonly IBackpackService _backpack;
    private readonly IMeasurementsService _measurements;
    private readonly IStatisticsCalculationService _statisticsCalculation;
    private readonly IGlucoseStatusService _glucoseStatusService;
    private readonly IGlucoseUiStateService _glucoseUiStateService;
    private readonly IPdfExportService _pdfExportService;

    public BotServiceDataController(
        IBotUserContextService botContext,
        IBackpackService backpack,
        IMeasurementsService measurements,
        IStatisticsCalculationService statisticsCalculation,
        IGlucoseStatusService glucoseStatusService,
        IGlucoseUiStateService glucoseUiStateService,
        IPdfExportService pdfExportService)
    {
        _botContext = botContext;
        _backpack = backpack;
        _measurements = measurements;
        _statisticsCalculation = statisticsCalculation;
        _glucoseStatusService = glucoseStatusService;
        _glucoseUiStateService = glucoseUiStateService;
        _pdfExportService = pdfExportService;
    }

    [HttpGet("{telegramUserId:long}/children/{childId:guid}/backpack")]
    public async Task<ActionResult<BackpackResponse>> GetBackpack(
        long telegramUserId,
        Guid childId,
        CancellationToken cancellationToken)
    {
        var userId = await GetAuthorizedUserIdAsync(telegramUserId, childId, cancellationToken);
        if (!userId.HasValue)
            return Forbid();

        var backpack = await _backpack.GetAsync(childId, cancellationToken);
        return backpack is null
            ? this.ProblemWithCode(404, "Child Not Found", "Ребёнок не найден", "child_not_found")
            : Ok(backpack);
    }

    [HttpPost("{telegramUserId:long}/children/{childId:guid}/backpack")]
    public async Task<ActionResult<BackpackItemResponse>> AddBackpackItem(
        long telegramUserId,
        Guid childId,
        [FromBody] BotBackpackCreateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return ValidationProblem(ModelState);

        var userId = await GetAuthorizedUserIdAsync(telegramUserId, childId, cancellationToken);
        if (!userId.HasValue)
            return Forbid();

        var item = await _backpack.AddAsync(new CreateBackpackItemRequest
        {
            ChildId = childId,
            SnackName = request.SnackName.Trim(),
            BreadUnits = request.BreadUnits,
            AddedBy = "telegram-bot"
        }, userId.Value, cancellationToken);

        return CreatedAtAction(nameof(GetBackpack), new { telegramUserId, childId }, item);
    }

    [HttpDelete("{telegramUserId:long}/children/{childId:guid}/backpack/{itemId:guid}")]
    public async Task<IActionResult> RemoveBackpackItem(
        long telegramUserId,
        Guid childId,
        Guid itemId,
        CancellationToken cancellationToken)
    {
        var userId = await GetAuthorizedUserIdAsync(telegramUserId, childId, cancellationToken);
        if (!userId.HasValue)
            return Forbid();

        var item = await _backpack.GetByIdAsync(itemId, cancellationToken);
        if (item is null)
            return NotFound();
        if (item.ChildId != childId)
            return Forbid();

        var result = await _backpack.RemoveForVerifiedIntegrationAsync(itemId, userId.Value, cancellationToken);
        return result switch
        {
            BackpackRemoveResult.Removed => NoContent(),
            BackpackRemoveResult.NotFound => NotFound(),
            _ => Forbid()
        };
    }

    [HttpGet("{telegramUserId:long}/children/{childId:guid}/statistics")]
    public async Task<ActionResult<StatisticsResponse>> GetStatistics(
        long telegramUserId,
        Guid childId,
        [FromQuery] string period = "day",
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        var userId = await GetAuthorizedUserIdAsync(telegramUserId, childId, cancellationToken);
        if (!userId.HasValue)
            return Forbid();

        var statistics = await BuildStatisticsAsync(childId, period, date, cancellationToken);
        return statistics is null
            ? this.ProblemWithCode(404, "Child Not Found", "Ребёнок не найден", "child_not_found")
            : Ok(statistics);
    }

    [HttpGet("{telegramUserId:long}/children/{childId:guid}/statistics/pdf")]
    public async Task<IActionResult> ExportStatisticsPdf(
        long telegramUserId,
        Guid childId,
        [FromQuery] string period = "day",
        [FromQuery] DateTime? date = null,
        CancellationToken cancellationToken = default)
    {
        var userId = await GetAuthorizedUserIdAsync(telegramUserId, childId, cancellationToken);
        if (!userId.HasValue)
            return Forbid();

        var child = await _measurements.GetChildAsync(childId, cancellationToken);
        if (child is null)
            return this.ProblemWithCode(404, "Child Not Found", "Ребёнок не найден", "child_not_found");

        var statistics = await BuildStatisticsAsync(childId, period, date, cancellationToken);
        if (statistics is null)
            return NotFound();

        var childName = string.Join(' ', new[] { child.FirstName, child.LastName }.Where(value => !string.IsNullOrWhiteSpace(value)));
        var pdf = await _pdfExportService.GenerateStatisticsReportAsync(statistics, string.IsNullOrWhiteSpace(childName) ? "Ребёнок" : childName);
        return File(pdf, "application/pdf", $"SugarGuard_Report_{statistics.Period}_{DateTime.UtcNow:yyyyMMdd}.pdf");
    }

    private async Task<Guid?> GetAuthorizedUserIdAsync(long telegramUserId, Guid childId, CancellationToken cancellationToken)
    {
        var user = await _botContext.FindUserByTelegramIdAsync(telegramUserId, cancellationToken);
        if (user is null)
            return null;

        var childIsLinked = (await _botContext.GetLinkedChildrenAsync(user.UserId, cancellationToken))
            .Any(child => child.ChildId == childId);
        return childIsLinked ? user.UserId : null;
    }

    private async Task<StatisticsResponse?> BuildStatisticsAsync(
        Guid childId,
        string period,
        DateTime? date,
        CancellationToken cancellationToken)
    {
        var child = await _measurements.GetChildAsync(childId, cancellationToken);
        if (child is null)
            return null;

        var (fromDate, toDate, periodName) = _statisticsCalculation.GetPeriodRange(period, date ?? DateTime.UtcNow);
        var measurements = await _measurements.GetForStatisticsAsync(childId, fromDate, toDate, cancellationToken);
        var calculated = _statisticsCalculation.CalculateStatistics(measurements.ToList());

        return new StatisticsResponse
        {
            ChildId = childId,
            Period = periodName,
            FromDate = fromDate,
            ToDate = toDate,
            TotalMeasurements = calculated.TotalMeasurements,
            AverageGlucose = calculated.AverageGlucose,
            MinGlucose = calculated.MinGlucose,
            MaxGlucose = calculated.MaxGlucose,
            StandardDeviation = calculated.StandardDeviation,
            TimeInTargetRange = calculated.TimeInTargetRange,
            HypoEpisodes = calculated.HypoEpisodes,
            HyperEpisodes = calculated.HyperEpisodes,
            CriticalEpisodes = calculated.CriticalEpisodes,
            TimeZoneId = string.IsNullOrWhiteSpace(child.TimeZoneId) ? "Europe/Moscow" : child.TimeZoneId,
            Measurements = measurements.Select(item => item.ToResponse(_glucoseStatusService, _glucoseUiStateService)).ToList()
        };
    }
}
