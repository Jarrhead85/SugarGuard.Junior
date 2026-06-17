namespace SugarGuard.Junior.Services.Interfaces;

/// <summary>
/// Сервис для генерации и обработки кодов привязки родителей
/// Обеспечивает безопасную связь между приложением ребёнка и Telegram-ботом родителя
/// </summary>
/// <remarks>
/// SEC-2: сервис больше НЕ хеширует код перед отправкой. Хеширование
/// (HMAC-SHA256 с серверным ключом) выполняется на сервере в
/// <c>ParentLinkService.SaveConnectionCodeAsync</c> через
/// <c>IConnectionCodeHasher</c>. Клиент передаёт <b>сырой</b> код по TLS,
/// а хеш хранится в БД и недоступен злоумышленнику без серверного ключа.
/// </remarks>
public interface IConnectionCodeService
{
    /// <summary>
    /// Генерирует код привязки в формате <c>XXXX-YYYY</c> через
    /// <see cref="SugarGuard.Shared.Constants.ConnectionCodeFormat"/>.
    /// </summary>
    /// <returns>Код в формате <c>ABCD-1234</c>.</returns>
    string GenerateConnectionCode();

    /// <summary>
    /// Отправляет сырой код на сервер для сохранения. Сервер сам
    /// хеширует его HMAC-SHA256 с серверным ключом.
    /// </summary>
    /// <param name="childId">ID ребёнка.</param>
    /// <param name="code">Сырой 8-символьный код.</param>
    /// <returns><c>true</c>, если код успешно сохранён на сервере.</returns>
    Task<bool> SendCodeToServerAsync(string childId, string code);

    /// <summary>
    /// Генерирует код и отправляет его (сырым) на сервер.
    /// </summary>
    /// <param name="childId">ID ребёнка.</param>
    /// <returns>Сгенерированный код для отображения пользователю, или <c>null</c> при ошибке.</returns>
    Task<string?> GenerateAndSendCodeAsync(string childId);
}