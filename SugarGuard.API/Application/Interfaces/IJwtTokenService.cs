using SugarGuard.Domain.Entities;

namespace SugarGuard.API.Application.Interfaces;

public interface IJwtTokenService
{
    /// <summary>
    /// Генерирует JWT с claims UserId и при необходимости TelegramId
    /// </summary>
    string GenerateToken(User user);
}
