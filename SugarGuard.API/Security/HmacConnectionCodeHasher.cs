using System.Security.Cryptography;
using System.Text;
using SugarGuard.Shared.Constants;

namespace SugarGuard.API.Security;

/// <summary>
/// Хешер для кодов привязки
/// </summary>
public sealed class HmacConnectionCodeHasher : IConnectionCodeHasher
{
    private const int MinKeySizeBytes = 32;

    private readonly byte[] _key;
    private readonly ILogger<HmacConnectionCodeHasher> _logger;

    /// <summary>
    /// Инициализирует хешер ключом из конфигурации
    /// </summary>
    public HmacConnectionCodeHasher(
        IConfiguration configuration,
        ILogger<HmacConnectionCodeHasher> logger)
    {
        _logger = logger;

        var keyBase64 = Environment.GetEnvironmentVariable("CONNECTION_CODE_KEY")
            ?? configuration["Crypto:ConnectionCodeKey"];

        if (string.IsNullOrWhiteSpace(keyBase64))
            throw new InvalidOperationException(
                "CONNECTION_CODE_KEY не задан. Задайте переменную окружения или Crypto:ConnectionCodeKey.");

        byte[] keyBytes;
        try
        {
            keyBytes = Convert.FromBase64String(keyBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                "CONNECTION_CODE_KEY должен быть корректной Base64-строкой.", ex);
        }

        if (keyBytes.Length < MinKeySizeBytes)
            throw new InvalidOperationException(
                $"CONNECTION_CODE_KEY должен быть не менее {MinKeySizeBytes} байт (256 бит). " +
                $"Получено: {keyBytes.Length} байт.");

        _key = keyBytes;

        _logger.LogInformation(
            "HmacConnectionCodeHasher: инициализирован (KeySize={KeySize} байт).",
            _key.Length);
    }

    /// <inheritdoc/>
    public string Hash(string code)
    {
        if (string.IsNullOrEmpty(code))
            throw new ArgumentException("Код не может быть пустым.", nameof(code));

        var normalizedCode = ConnectionCodeFormat.Normalize(code)
            ?? throw new ArgumentException("Код должен быть в формате ABCD-1234.", nameof(code));

        var codeBytes = Encoding.UTF8.GetBytes(normalizedCode);

        using var hmac = new HMACSHA256(_key);
        var hashBytes = hmac.ComputeHash(codeBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
