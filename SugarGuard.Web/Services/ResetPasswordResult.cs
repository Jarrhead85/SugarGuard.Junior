namespace SugarGuard.Web.Services;

/// <summary>
/// Результат операции сброса пароля через POST /api/auth/reset-password
/// </summary>
public sealed class ResetPasswordResult
{   
    public bool IsSuccess { get; init; } // Операция выполнена успешно
   
    public string? Message { get; init; } // Сообщение от API (описание ошибки или подтверждение)
   
    public static ResetPasswordResult Ok(string? message = null) =>
        new() { IsSuccess = true, Message = message }; // Вспомогательный метод: результат успеха
   
    public static ResetPasswordResult Fail(string? message) =>
        new() { IsSuccess = false, Message = message }; // Вспомогательный метод: результат ошибки
}
