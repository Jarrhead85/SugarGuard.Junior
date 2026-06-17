using System.ComponentModel.DataAnnotations;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Запрос на генерацию кода приглашения
    /// </summary>
    public class GenerateInviteCodeRequest
    {
        [Required]
        public Guid ChildId { get; set; } // ID ребёнка-инициатора

        [Required]
        public UserRole TargetRole { get; set; } // Роль, для которой выпускается код
    }
}
