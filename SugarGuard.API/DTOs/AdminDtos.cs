using System.ComponentModel.DataAnnotations;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Запрос на создание административного инвайт-кода
/// </summary>
public sealed class CreateAdminInvitationRequest
{
    [Required]
    public Guid ChildId { get; init; } // ID ребёнка-инициатора приглашения

    [Required]
    public UserRole TargetRole { get; init; } // Целевая роль
}

/// <summary>
/// Запись аудит-лога
/// </summary>
public sealed class AuditLogResponse
{   
    public Guid AuditLogId { get; init; } // ID записи
   
    public Guid? ActorUserId { get; init; } // ID пользователя, совершившего действие
   
    public string Action { get; init; } = string.Empty; // Код действия
   
    public string? TargetType { get; init; } // Тип затронутой сущности
   
    public string? TargetId { get; init; } // ID затронутой сущности
   
    public string? Details { get; init; } // Дополнительные детали события
   
    public DateTime CreatedAt { get; init; } // Время события
}

/// <summary>
/// Сводка по воронке онбординга
/// </summary>
public sealed class OnboardingFunnelResponse
{   
    public string? FilterRole { get; init; } // Фильтр по роли, если был применён
   
    public DateTime? FilterFrom { get; init; } // Начало временного диапазона фильтра
   
    public DateTime? FilterTo { get; init; } // Конец временного диапазона фильтра
   
    public int TotalStarted { get; init; } // Количество уникальных пользователей, начавших онбординг
   
    public int TotalCompleted { get; init; } // Количество уникальных пользователей, завершивших онбординг
   
    public double ConversionRate { get; init; } // Конверсия: доля завершивших от начавших

    // Разбивка по шагам воронки
    public IReadOnlyList<OnboardingFunnelStepDto> Steps { get; init; }
        = Array.Empty<OnboardingFunnelStepDto>();
}

/// <summary>
/// Один шаг воронки онбординга
/// </summary>
public sealed class OnboardingFunnelStepDto
{
    public int StepNumber { get; init; } // Номер шага онбординга
   
    public int UniqueUsers { get; init; } // Количество уникальных пользователей, достигших этого шага

    public double RetentionRate { get; init; } // Показатель удержания
}
public class AdminUserResponse
{
    public Guid UserId { get; init; }
    public string? EmailForLogin { get; init; }
    public long? TelegramId { get; init; }
    public string Role { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string? PhotoUrl { get; init; }
    public string? Specialty { get; init; }
    public string? LicenseNumber { get; init; }
    public bool IsEmailVerified { get; init; }
}

public class UpdateUserRoleRequest
{
    public string NewRole { get; init; } = string.Empty;
}

/// <summary>
/// Запрос на массовое изменение статуса учётных записей.
/// </summary>
public sealed class UpdateUsersActivityRequest
{
    [Required]
    [MinLength(1)]
    [MaxLength(100)]
    public IReadOnlyList<Guid> UserIds { get; init; } = [];

    public bool IsActive { get; init; }
}

/// <summary>
/// Результат массового изменения статуса учётных записей.
/// </summary>
public sealed class UpdateUsersActivityResponse
{
    public int UpdatedCount { get; init; }
    public bool IsActive { get; init; }
}
