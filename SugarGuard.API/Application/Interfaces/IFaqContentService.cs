using SugarGuard.API.DTOs;
using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Бизнес-логика CRUD для FAQ-статей
/// </summary>
public interface IFaqContentService
{
    /// <summary>
    /// Возвращает только опубликованные статьи, отсортированные по дате обновления
    /// </summary>
    Task<IReadOnlyList<FaqArticleResponse>> GetPublishedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Возвращает все статьи (для админ-панели)
    /// </summary>
    Task<IReadOnlyList<FaqArticleResponse>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Создаёт новую FAQ-статью
    /// </summary>
    Task<FaqArticleResponse> CreateAsync(FaqArticleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Обновляет FAQ-статью по ID
    /// </summary>
    Task<FaqArticleResponse?> UpdateAsync(Guid id, FaqArticleRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Удаляет FAQ-статью по ID
    /// </summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
