namespace SugarGuard.API.Security;

/// <summary>
/// Контракт серверного криптосервиса для шифрования PHI-данных
/// </summary>
public interface ICryptoService
{
    string Encrypt(string plainText);

    string Decrypt(string encryptedBase64);
}
