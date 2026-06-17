using System.ComponentModel.DataAnnotations;
using SugarGuard.Shared.Validation;

namespace SugarGuard.API.DTOs
{
    /// <summary>
    /// Запрос на принятие кода приглашения
    /// </summary>
    public class ClaimInviteCodeRequest
    {
        /// <summary>
        /// 8-символьный код, введённый пользователем.
        /// </summary>
        [Required]
        [StringLength(9, MinimumLength = 8)]
        [ConnectionCode]
        public string Code { get; set; } = string.Empty;
    }
}
