using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Constants;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Сервис для генерации и обработки кодов привязки родителей.
/// Реализует безопасную связь между приложением ребёнка и Telegram-ботом родителя.
/// </summary>
/// <remarks>
/// SEC-2: сервис больше НЕ хеширует код перед отправкой. Хеширование
/// (HMAC-SHA256 с серверным ключом) выполняется на сервере в
/// <c>ParentLinkService.SaveConnectionCodeAsync</c>. Клиент передаёт
/// <b>сырой</b> код по TLS, а хеш хранится в БД и недоступен
/// злоумышленнику без серверного ключа.
/// </remarks>
public class ConnectionCodeService : IConnectionCodeService
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<ConnectionCodeService> _logger;

    public ConnectionCodeService(
        IApiClient apiClient,
        ILogger<ConnectionCodeService> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    /// <summary>
    /// Генерирует криптостойкий код привязки через <see cref="ConnectionCodeFormat"/>.
    /// </summary>
    public string GenerateConnectionCode()
    {
        try
        {
            var code = ConnectionCodeFormat.Format(ConnectionCodeFormat.Generate());
            _logger.LogInformation(" Сгенерирован код привязки");
            return code;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при генерации кода");
            throw;
        }
    }

    /// <summary>
    /// Отправляет сырой код на сервер для сохранения. Сервер сам
    /// хеширует его HMAC-SHA256 с серверным ключом.
    /// </summary>
    public async Task<bool> SendCodeToServerAsync(string childId, string code)
    {
        try
        {
            if (string.IsNullOrEmpty(childId))
                throw new ArgumentException("ID ребёнка не может быть пустым", nameof(childId));

            if (string.IsNullOrEmpty(code))
                throw new ArgumentException("Код не может быть пустым", nameof(code));

            var request = new SaveConnectionCodeRequest
            {
                ChildId = childId,
                Code = code
            };

            var response = await _apiClient.SaveConnectionCodeAsync(request);

            if (response.Success)
            {
                _logger.LogInformation("Код успешно отправлен на сервер");
                return true;
            }
            else
            {
                _logger.LogWarning("Не удалось отправить код: {ErrorMessage}", response.ErrorMessage);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при отправке кода: {Message}", ex.Message);
            return false;
        }
    }

    /// <summary>
    /// Генерирует код и отправляет его (сырым) на сервер. Сервер сам хеширует.
    /// </summary>
    public async Task<string?> GenerateAndSendCodeAsync(string childId)
    {
        try
        {
            _logger.LogInformation("Создание кода привязки для родителя...");

            // 1. Генерируем код
            var code = GenerateConnectionCode();

            // 2. Отправляем сырой код — сервер сам хеширует HMAC-SHA256
            var success = await SendCodeToServerAsync(childId, code);

            if (success)
            {
                _logger.LogInformation("Код привязки создан и отправлен на сервер");
                return code;
            }
            else
            {
                _logger.LogError("Не удалось создать код привязки");
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ошибка при создании кода привязки: {Message}", ex.Message);
            return null;
        }
    }
}
