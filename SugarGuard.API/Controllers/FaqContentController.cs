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
    private readonly IUploadPathProvider _uploadPaths;
    private readonly ILogger<FaqContentController> _logger;

    public FaqContentController(
        IFaqContentService faq,
        IUploadPathProvider uploadPaths,
        ILogger<FaqContentController> logger)
    {
        _faq = faq;
        _uploadPaths = uploadPaths;
        _logger = logger;
    }

    /// <summary>
    /// Создаёт статью, подготовленную врачом. Врач не получает прав на изменение
    /// чужих материалов, а созданная им статья сразу доступна в базе знаний.
    /// </summary>
    [Authorize(Roles = "Doctor,Admin,SupportAdmin")]
    [HttpPost("doctor")]
    [ProducesResponseType(typeof(FaqArticleResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<FaqArticleResponse>> CreateByDoctor(
        [FromBody] FaqArticleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        request.IsPublished = true;
        var result = await _faq.CreateAsync(request, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Принимает иллюстрацию для статьи. Формат и сигнатура файла проверяются
    /// до записи на диск, а ссылка ограничена каталогом статей.
    /// </summary>
    [Authorize(Roles = "Doctor,Admin,SupportAdmin")]
    [HttpPost("images")]
    [RequestSizeLimit(5 * 1024 * 1024)]
    public async Task<ActionResult<FaqImageUploadResponse>> UploadImage(
        IFormFile file,
        CancellationToken cancellationToken)
    {
        if (file.Length is <= 0 or > 5 * 1024 * 1024)
        {
            return BadRequest(new { error = "invalid_file_size", message = "Размер изображения должен быть не больше 5 МБ." });
        }

        await using var input = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, cancellationToken);
        var bytes = buffer.ToArray();
        var extension = DetectImageExtension(bytes);
        if (extension is null)
        {
            return BadRequest(new { error = "invalid_image", message = "Допустимы изображения JPEG, PNG и WebP." });
        }

        Directory.CreateDirectory(_uploadPaths.ArticleImagesDirectory);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var path = _uploadPaths.GetArticleImageFilePath(fileName);
        await System.IO.File.WriteAllBytesAsync(path, bytes, cancellationToken);

        return Ok(new FaqImageUploadResponse { ImageUrl = $"/uploads/articles/{fileName}" });
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

    private static string? DetectImageExtension(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })) return ".png";
        if (bytes.Length >= 3 && bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF) return ".jpg";
        if (bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8)) return ".webp";
        return null;
    }
}
