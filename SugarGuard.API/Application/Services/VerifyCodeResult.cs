namespace SugarGuard.API.Application.Services
{
    /// <summary>
    /// Результат проверки верификационного кода, введённого пользователем
    /// </summary>
    public class VerifyCodeResult
    {
       
        public bool Success { get; set; } // Верен ли введённый код       

        public string? ErrorCode { get; set; } // Машиночитаемый код ошибки
                                               // 
        public string? ErrorMessage { get; set; } // Читаемое сообщение об ошибке       

        public int? AttemptsLeft { get; set; } // Количество оставшихся попыток ввода кода       

        public string? VerificationToken { get; set; } // Одноразовый токен подтверждения email

        /// <summary>
        /// Фабричный метод для успешного результата
        /// </summary>
        public static VerifyCodeResult Ok(string verificationToken) =>
            new()
            {
                Success = true,
                VerificationToken = verificationToken
            };

        /// <summary>
        /// Фабричный метод для ошибки
        /// </summary>
        public static VerifyCodeResult Fail(
            string errorCode,
            string errorMessage,
            int? attemptsLeft = null) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                AttemptsLeft = attemptsLeft
            };
    }
}
