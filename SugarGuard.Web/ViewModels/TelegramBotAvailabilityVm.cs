namespace SugarGuard.Web.ViewModels;

/// <summary>
/// Непривилегированный статус Telegram-канала для кабинета родителя.
/// </summary>
public sealed record TelegramBotAvailabilityVm(
    bool IsAvailable,
    DateTime? LastCheckedAt,
    string Message);
