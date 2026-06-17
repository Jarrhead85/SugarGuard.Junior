using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Data;

namespace SugarGuard.API.Application.Interfaces;

/// <summary>
/// Сервис проверки здоровья приложения
/// </summary>
public interface IHealthService
{
    Task<bool> CanConnectAsync(CancellationToken cancellationToken = default); // Проверяет, доступна ли БД.
}
