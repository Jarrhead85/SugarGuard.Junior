namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Результат принятия кода приглашения пользователем
    /// </summary>
    public class ClaimInviteCodeResult
    {       
        public bool Success { get; set; } // Успешно ли принят код

        public string? ErrorCode { get; set; } // Код ошибки
       
        public string? ErrorMessage { get; set; } // Читаемое сообщение об ошибке для отображения в UI
       
        public Guid? ChildId { get; set; } // ID ребёнка из принятого кода

        public Guid? LinkId { get; set; } // ID созданной связки

        public string? LinkType { get; set; } // Тип созданной связки

        // Фабричные методы
        /// <summary>
        /// Создаёт успешный результат с данными созданной связки
        /// </summary>
        public static ClaimInviteCodeResult Ok(Guid childId, Guid linkId, string linkType) =>
            new()
            {
                Success = true,
                ChildId = childId,
                LinkId = linkId,
                LinkType = linkType
            };

        /// <summary>
        /// Создаёт результат с ошибкой
        /// </summary>
        public static ClaimInviteCodeResult Fail(string errorCode, string errorMessage) =>
            new()
            {
                Success = false,
                ErrorCode = errorCode,
                ErrorMessage = errorMessage
            };
    }
}
