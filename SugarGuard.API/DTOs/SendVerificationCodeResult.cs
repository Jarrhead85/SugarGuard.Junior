namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Результат отправки верификационного кода на email
    /// </summary>
    public class SendVerificationCodeResult
    {       
        public bool Success { get; set; } // Успешно ли отправлен код

        public string? ErrorCode { get; set; } // Код ошибки
                                               // 
        public string? ErrorMessage { get; set; } // Читаемое сообщение об ошибке

        public DateTime? ExpiresAt { get; set; } // UTC-время истечения кода

        public int RetryAfterSeconds { get; set; } = 60; // Через сколько секунд можно запросить повторную отправку

        /// <summary>
        /// Фабричный метод для успешного результата
        /// </summary>
        public static SendVerificationCodeResult Ok(DateTime expiresAt) =>
            new()
            {
                Success = true,
                ExpiresAt = expiresAt
            };

        /// <summary>
        /// Фабричный метод для ошибки
        /// </summary>
        public static SendVerificationCodeResult Fail(string errorCode, string errorMessage) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
    }
}
