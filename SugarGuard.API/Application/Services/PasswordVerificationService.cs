using SugarGuard.API.Application.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Проверка пароля
/// </summary>
public class PasswordVerificationService : IPasswordVerificationService
{
    private const int HashSize = 32;

    public bool VerifyPassword(string password, string hashBase64, string saltBase64)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(hashBase64) || string.IsNullOrEmpty(saltBase64))
            return false;

        try
        {
            var saltBytes = Convert.FromBase64String(saltBase64);
            using var pbkdf2 = new Rfc2898DeriveBytes(
                password,
                saltBytes,
                600_000,
                HashAlgorithmName.SHA256);
            var computedHash = pbkdf2.GetBytes(HashSize);
            var storedHash = Convert.FromBase64String(hashBase64);
            return CryptographicOperations.FixedTimeEquals(computedHash, storedHash);
        }
        catch
        {
            return false;
        }
    }
}
