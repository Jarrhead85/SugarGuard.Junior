using SugarGuard.Web.Services;

namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Health-check из Admin-раздела (GET /api/admin/system/health)
/// </summary>
public sealed class AdminHealthVm
{   
    public string   Status     { get; init; } = string.Empty; // Статус   
    public bool     DatabaseOk { get; init; } // true — БД доступна
    public bool     Database => DatabaseOk;
    public DateTime? ServerUtc { get; init; }

    /// <summary>
    /// Создаёт VM из транспортного DTO сервиса
    /// </summary>
    internal static AdminHealthVm FromDto(AdminHealthDto dto) => new()
    {
        Status     = dto.Status ?? string.Empty,
        DatabaseOk = dto.DatabaseOk,
        ServerUtc  = dto.ServerUtc
    };
}
