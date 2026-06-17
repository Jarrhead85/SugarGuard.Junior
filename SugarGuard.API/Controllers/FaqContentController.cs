using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.DTOs;
using SugarGuard.API.Extensions;

namespace SugarGuard.API.Controllers;

/// <summary>
/// Контроллер CRUD для FAQ-статей
/// </summary>
[ApiController]
[Route("api/faq-content")]
[Produces("application/json")]
public class FaqContentController : ControllerBase
{
    private readonly IFaqContentService _faq;
    private readonly ILogger<FaqContentController> _logger;

    public FaqContentController(
        IFaqContentService faq,
        ILogger<FaqContentController> logger)
    {
        _faq = faq;
        _logger = logger;
    }

    /// <summary>
    /// Возвращает только опубликованные FAQ-статьи
    /// </summary>
    [Authorize(Policy = "ParentOrDoctorOrAdmin")]
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<FaqArticleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FaqArticleResponse>>> GetPublished(
        CancellationToken cancellationToken)
    {
        var items = await _faq.GetPublishedAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Возвращает все FAQ-статьи (включая черновики)
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpGet("admin")]
    [ProducesResponseType(typeof(IReadOnlyList<FaqArticleResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<FaqArticleResponse>>> GetAll(
        CancellationToken cancellationToken)
    {
        var items = await _faq.GetAllAsync(cancellationToken);
        return Ok(items);
    }

    /// <summary>
    /// Создаёт новую FAQ-статью
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [ProducesResponseType(typeof(FaqArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<FaqArticleResponse>> Create(
        [FromBody] FaqArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });
        }

        try
        {
            var result = await _faq.CreateAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FaqContent.Create: ошибка при создании FAQ-статьи.");
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось создать FAQ-статью." });
        }
    }

    /// <summary>
    /// Обновляет FAQ-статью
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(FaqArticleResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FaqArticleResponse>> Update(
        Guid id,
        [FromBody] FaqArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(new
            {
                error = "validation_error",
                details = ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage)
            });
        }

        try
        {
            var result = await _faq.UpdateAsync(id, request, cancellationToken);
            if (result is null)
            {
                return this.ProblemWithCode(404, "FAQ Not Found",
                    "FAQ-статья не найдена", "faq_not_found");
            }
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FaqContent.Update: ошибка при обновлении FAQ-статьи {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось обновить FAQ-статью." });
        }
    }

    /// <summary>
    /// Удаляет FAQ-статью
    /// </summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var deleted = await _faq.DeleteAsync(id, cancellationToken);
            if (!deleted)
            {
                return this.ProblemWithCode(404, "FAQ Not Found",
                    "FAQ-статья не найдена", "faq_not_found");
            }
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "FaqContent.Delete: ошибка при удалении FAQ-статьи {Id}.", id);
            return StatusCode(StatusCodes.Status500InternalServerError,
                new { error = "internal_error", message = "Не удалось удалить FAQ-статью." });
        }
    }
}
