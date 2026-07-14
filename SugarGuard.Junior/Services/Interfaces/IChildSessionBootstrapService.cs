namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Восстанавливает локальный контекст ребёнка для уже авторизованного аккаунта.
/// </summary>
public interface IChildSessionBootstrapService
{
    /// <summary>
    /// Гарантирует, что в локальном хранилище выбран существующий серверный ребёнок.
    /// Возвращает <c>false</c>, только если у аккаунта действительно нет профиля ребёнка.
    /// </summary>
    Task<bool> EnsureChildSessionAsync(CancellationToken cancellationToken = default);
}
