using System.ComponentModel.DataAnnotations;

namespace SugarGuard.API.DTOs;

/// <summary>
/// Полный профиль ребёнка для API
/// </summary>
public sealed class ChildResponse
{
    public Guid ChildId { get; init; }
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public DateOnly DateOfBirth { get; init; }
    public decimal Weight { get; init; }
    public decimal Height { get; init; }
    public string DiabetesType { get; init; } = string.Empty;
    public DateOnly? DiagnosisDate { get; init; }
    public string? InsulinScheme { get; init; }
    public string CurrentInsulins { get; init; } = "[]";
    public string TimeZoneId { get; init; } = "UTC";
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public string? PhotoUrl { get; init; }
}

/// <summary>
/// Результат создания ребёнка с опциональной связью родителя
/// </summary>
public sealed class CreateChildResult
{
    public ChildResponse Child { get; init; } = null!;
    public Guid? ParentLinkId { get; init; }
}

/// <summary>
/// Результат загрузки фото ребёнка
/// </summary>
public sealed class ChildPhotoUploadResponse
{   
    public string PhotoUrl { get; init; } = string.Empty; // Публичный URL нового фото
}

/// <summary>
/// Запрос на создание профиля ребёнка
/// </summary>
public sealed class CreateChildRequest
{
    [Required]
    [MaxLength(255)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; init; }

    [Required]
    [MaxLength(50)]
    public string DiabetesType { get; init; } = string.Empty;

    public DateOnly? DiagnosisDate { get; init; }

    [Range(5.0, 200.0)]
    public decimal? Weight { get; init; }

    [Range(50.0, 250.0)]
    public decimal? Height { get; init; }

    [MaxLength(500)]
    public string? InsulinScheme { get; init; }

    [MaxLength(100)]
    public string TimeZoneId { get; init; } = "UTC";

    [MaxLength(500)]
    public string? PhotoUrl { get; init; }// URL фото 
}

/// <summary>
/// Запрос на обновление профиля ребёнка
/// </summary>
public sealed class UpdateChildRequest
{
    [Required]
    [MaxLength(255)]
    public string FirstName { get; init; } = string.Empty;

    [Required]
    [MaxLength(255)]
    public string LastName { get; init; } = string.Empty;

    [Required]
    public DateOnly DateOfBirth { get; init; }

    [Required]
    [MaxLength(50)]
    public string DiabetesType { get; init; } = string.Empty;

    public DateOnly? DiagnosisDate { get; init; }

    [Range(5.0, 200.0)]
    public decimal Weight { get; init; }

    [Range(50.0, 250.0)]
    public decimal Height { get; init; }

    [MaxLength(500)]
    public string? InsulinScheme { get; init; }

    [MaxLength(100)]
    public string TimeZoneId { get; init; } = "UTC";

    [MaxLength(500)]
    public string? PhotoUrl { get; init; } // URL фото
}

/// <summary>
/// Медицинские настройки диабета ребёнка
/// </summary>
public sealed class DiabetesSettingsResponse
{
    public Guid ChildId { get; init; }
    public decimal TargetRangeMin { get; init; }
    public decimal TargetRangeMax { get; init; }
    public decimal InsulinSensitivity { get; init; }
    public decimal CarbInsulinRatio { get; init; }
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// Запрос на обновление
/// </summary>
public sealed class UpdateDiabetesSettingsRequest
{   
    [Range(2.0, 15.0)]
    public decimal TargetRangeMin { get; init; } // Нижняя граница целевого диапазона
   
    [Range(2.0, 20.0)]
    public decimal TargetRangeMax { get; init; } // Верхняя граница целевого диапазон
   
    [Range(0.1, 20.0)]
    public decimal InsulinSensitivity { get; init; } // Фактор чувствительности к инсулину
   
    [Range(0.1, 50.0)]
    public decimal CarbInsulinRatio { get; init; } // Углеводный коэффициент
}
