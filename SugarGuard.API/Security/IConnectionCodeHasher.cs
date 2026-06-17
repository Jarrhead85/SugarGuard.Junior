namespace SugarGuard.API.Security;

/// <summary>
/// Контракт серверного хешера кодов привязки
/// </summary>

public interface IConnectionCodeHasher
{
    string Hash(string code);
}
