using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using SugarGuard.Domain.Enums;

namespace SugarGuard.Domain.Entities
{
    /// <summary>
    /// Одноразовый код приглашения для связки ребёнка с родителем или врачом
    /// </summary>
    [Table("invitationcodes")]
    public class InviteCode
    {
        [Key]
        [Column("invitecodeid")]
        public Guid InviteCodeId { get; set; } = Guid.NewGuid(); // Первичный ключ

        [Required]
        [Column("childid")]
        public Guid ChildId { get; set; } // ID ребёнка-инициатора

        [Required]
        [MaxLength(8)]
        [Column("code")]
        public string Code { get; set; } = string.Empty; // Буквенно-цифровой код

        [Required]
        [Column("targetrole")]
        public UserRole TargetRole { get; set; } // Роль, для которой выпущен код

        [Required]
        [MaxLength(16)]
        [Column("status")]
        public string Status { get; set; } = "Pending"; // Статус кода

        [Required]
        [Column("expiresat")]
        public DateTime ExpiresAt { get; set; } // время истечения кода

        [Column("claimedbydyuserid")]
        public Guid? ClaimedByUserId { get; set; } // ID пользователя, который воспользовался кодом

        [Column("createdat")]
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow; // время создания

        // Навигационные свойства 
        [ForeignKey(nameof(ChildId))]
        public virtual Child Child { get; set; } = null!; // Ребёнок-инициатор приглашения

        [ForeignKey(nameof(ClaimedByUserId))]
        public virtual User? ClaimedByUser { get; set; } // Пользователь, принявший приглашение

        // Вычисляемые свойства (не в БД)
        [NotMapped]
        public bool IsExpired => DateTime.UtcNow > ExpiresAt; // Истёк ли код по времени

        [NotMapped]
        public bool IsActive => Status == "Pending" && !IsExpired; // Активен ли код
    }
}
