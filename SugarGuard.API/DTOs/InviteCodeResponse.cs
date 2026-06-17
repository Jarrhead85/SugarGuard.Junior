using SugarGuard.Domain.Enums;

namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// DTO для отображения кода приглашения клиенту
    /// </summary>
    public class InviteCodeResponse
    {       
        public Guid InviteCodeId { get; set; } // Первичный ключ записи в БД
       
        public Guid ChildId { get; set; } // ID ребёнка-инициатора

        public string Code { get; set; } = string.Empty; // 8-символьный буквенно-цифровой код для ввода на сайте
       
        public UserRole TargetRole { get; set; } // Роль, для которой выпущен код
       
        public string Status { get; set; } = string.Empty; // Текущий статус
       
        public DateTime ExpiresAt { get; set; } // UTC-время истечения кода
       
        public DateTime CreatedAt { get; set; } // UTC-время создания кода

        public bool IsActive { get; set; } // Активен ли код прямо сейчас
    }
}
