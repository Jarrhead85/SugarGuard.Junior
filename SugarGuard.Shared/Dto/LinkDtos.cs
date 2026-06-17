using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using SugarGuard.Shared.Constants;
using SugarGuard.Shared.Validation;

namespace SugarGuard.Shared.Dto;

// Генерация кода приглашения
/// <summary>
/// Запрос на генерацию кода приглашения для привязки нового пользователя к ребёнку
/// </summary>
public sealed class GenerateInviteCodeRequest
{
    [Required]
    public Guid ChildId { get; init; } // Идентификатор ребёнка, к которому приглашают нового участника

    [Required]
    [MaxLength(32)]
    public string TargetRole { get; init; } = string.Empty; // Роль, которую получит пользователь при принятии кода

    [MaxLength(256)]
    public string? Note { get; init; } // Необязательная заметка для получателя 
}

/// <summary>
/// Ответ на запрос генерации кода приглашения
/// </summary>
public sealed class GenerateInviteCodeResponse
{
    public Guid InviteCodeId { get; init; } // Уникальный идентификатор записи кода приглашения в базе данных

    public string DisplayCode { get; init; } = string.Empty; // Отформатированный код для передачи получателю

    public DateTime ExpiresAt { get; init; } // Дата и время истечения кода

    public int ActiveCodesCount { get; init; } // Количество активных кодов у данного ребёнка после генерации

    public string TargetRole { get; init; } = string.Empty; // Роль, которую получит пользователь при принятии кода

    public string? ErrorMessage { get; init; } // Сообщение об ошибке
}

// Принятие кода приглашения
/// <summary>
/// Запрос на принятие кода приглашения
/// </summary>
public sealed class AcceptInviteCodeRequest
{
    [Required]
    [StringLength(9, MinimumLength = 8)]
    [ConnectionCode]
    public string Code { get; init; } = string.Empty; // Код приглашения
}

/// <summary>
/// Ответ на принятие кода приглашения
/// </summary>
public sealed class AcceptInviteCodeResponse
{
    public bool Success { get; init; } // Признак успешного принятия кода и создания связи

    public Guid? LinkId { get; init; } // Идентификатор созданной связи

    public Guid? ChildId { get; init; } // Идентификатор ребёнка, к которому привязан пользователь

    public string? ChildFirstName { get; init; } // Имя ребёнка для отображения в приветственном сообщении

    public string? AssignedRole { get; init; } // Роль, которую получил пользователь в результате привязки

    public string? Message { get; init; } // Сообщение для отображения пользователю

    public string? ErrorMessage { get; init; } // Сообщение об ошибке
}

// Просмотр и отзыв кодов приглашений
/// <summary>
/// Краткая информация об одном активном коде приглашения
/// </summary>
public sealed class InviteCodeSummaryDto
{
    public Guid InviteCodeId { get; init; } // Идентификатор кода приглашения

    public string TargetRole { get; init; } = string.Empty; // Целевая роль получателя

    public DateTime CreatedAt { get; init; } // Дата и время создания кода

    public DateTime ExpiresAt { get; init; } // Дата и время истечения кода

    public string Status { get; init; } = string.Empty; // Текущий статус кода

    public string? Note { get; init; } // Необязательная заметка, указанная при создании

    public int FailedAttempts { get; init; } // Количество неверных попыток ввода этого кода
}

/// <summary>
/// Запрос на отзыв кода приглашения
/// </summary>
public sealed class RevokeInviteCodeRequest
{
    [Required]
    public Guid InviteCodeId { get; init; } // Идентификатор кода, который нужно аннулировать
}

/// <summary>
/// Ответ на отзыв кода приглашения
/// </summary>
public sealed class RevokeInviteCodeResponse
{
    public bool Success { get; init; } // Признак успешного аннулирования

    public string? ErrorMessage { get; init; } // Сообщение об ошибке
}

// Просмотр существующих связей
/// <summary>
/// Информация об одной связи родитель–ребёнок
/// </summary>
public sealed class ParentChildLinkDto
{
    public Guid LinkId { get; init; } // Идентификатор связи

    public Guid ParentUserId { get; init; } // Идентификатор пользователя-родителя

    public string? ParentEmail { get; init; } // Email родителя для отображения в административной таблице

    public string? ParentTelegramUsername { get; init; } // Telegram-имя родителя

    public Guid ChildId { get; init; } // Идентификатор ребёнка

    public DateTime CreatedAt { get; init; } // Дата и время создания связи
}

/// <summary>
/// Информация об одной связи врач–ребёнок
/// </summary>
public sealed class DoctorChildLinkDto
{
    public Guid LinkId { get; init; } // Идентификатор связи

    public Guid DoctorUserId { get; init; } // Идентификатор пользователя-врача

    public string? DoctorEmail { get; init; } // Email врача для отображения в административной таблице

    public Guid ChildId { get; init; } // Идентификатор ребёнка

    public DateTime CreatedAt { get; init; } // Дата и время создания связи
}

// Административные операции с прямым созданием/удалением связей

/// <summary>
/// Запрос на прямое создание связи родитель–ребёнок администратором
/// </summary>
public sealed class CreateParentChildLinkRequest
{
    [Required]
    public Guid ParentUserId { get; init; } // Идентификатор пользователя с ролью Parent

    [Required]
    public Guid ChildId { get; init; } // Идентификатор ребёнка
}

/// <summary>
/// Запрос на прямое создание связи врач–ребёнок администратором
/// </summary>
public sealed class CreateDoctorChildLinkRequest
{
    [Required]
    public Guid DoctorUserId { get; init; } // Идентификатор пользователя с ролью Doctor

    [Required]
    public Guid ChildId { get; init; } // Идентификатор ребёнка
}

/// <summary>
/// Унифицированный ответ на операции создания/удаления связей администратором
/// </summary>
public sealed class LinkOperationResponse
{
    public bool Success { get; init; } // Признак успешного выполнения операции

    public Guid? LinkId { get; init; } // Идентификатор созданной или удалённой связи

    public string? ErrorMessage { get; init; } // Сообщение об ошибке
}

// Статусы и вспомогательные константы
/// <summary>
/// Строковые константы статусов кода приглашения
/// </summary>
public static class InviteCodeStatus
{
    public const string Pending = "Pending";
    public const string Claimed = "Claimed";
    public const string Expired = "Expired";
    public const string Rejected = "Rejected";
}
