// Модель данных о ребёнке
using SugarGuard.Junior.Core.Security;
using SugarGuard.Junior.Models.Enums;

namespace SugarGuard.Junior.Models.Core;

public class Child
{
    /// <summary>
    /// Уникальный идентификатор ребёнка
    /// </summary>
    public string ChildId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// ID родителя, к которому привязан ребёнок
    /// </summary>
    public string ParentUserId { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованная фамилия ребёнка
    /// </summary>
    public string EncryptedLastName { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованное имя ребёнка
    /// </summary>
    public string EncryptedFirstName { get; set; } = string.Empty;

    /// <summary>
    /// Зашифрованная дата рождения
    /// </summary>
    public string EncryptedDateOfBirth { get; set; } = string.Empty;

    /// <summary>
    /// Дата рождения (не сохраняется — расшифровывается из EncryptedDateOfBirth)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime DateOfBirth { get; set; }

    /// <summary>
    /// Зашифрованный вес в кг
    /// </summary>
    public string EncryptedWeight { get; set; } = string.Empty;

    /// <summary>
    /// Вес (не сохраняется — расшифровывается из EncryptedWeight)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double Weight { get; set; }

    /// <summary>
    /// Зашифрованный рост в см
    /// </summary>
    public string EncryptedHeight { get; set; } = string.Empty;

    /// <summary>
    /// Рост (не сохраняется — расшифровывается из EncryptedHeight)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public double Height { get; set; }

    /// <summary>
    /// Путь к фото ребёнка
    /// </summary>
    public string? PhotoUrl { get; set; }

    /// <summary>
    /// Зашифрованный тип диабета
    /// </summary>
    public string EncryptedDiabetesType { get; set; } = string.Empty;

    /// <summary>
    /// Тип диабета (не сохраняется — расшифровывается из EncryptedDiabetesType)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DiabetesType DiabetesType { get; set; }

    /// <summary>
    /// Зашифрованная дата диагностирования
    /// </summary>
    public string EncryptedDiagnosisDate { get; set; } = string.Empty;

    /// <summary>
    /// Дата диагностирования (не сохраняется — расшифровывается из EncryptedDiagnosisDate)
    /// </summary>
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public DateTime DiagnosisDate { get; set; }

    /// <summary>
    /// Используемая схема инсулина (например: "Быстрый + длительный")
    /// </summary>
    public string InsulinScheme { get; set; } = string.Empty;

    /// <summary>
    /// Список текущих препаратов инсулина (JSON-массив)
    /// Например: ["Novolog", "Lantus"]
    /// </summary>
    public string CurrentInsulins { get; set; } = "[]";

    /// <summary>
    /// Дата создания профиля
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Дата последнего обновления профиля
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Версия шифрования зашифрованных полей этой записи (см. <c>EncryptionVersion</c>).
    /// Используется <see cref="VersionedEncryptionService"/> для dual-decrypt CBC/GCM при миграции.
    /// </summary>
    public EncryptionVersion EncryptionVersion { get; set; } = EncryptionVersion.AesGcm;

    /// <summary>
    /// Вычисленный возраст в годах
    /// </summary>
    public int AgeInYears
    {
        get
        {
            var today = DateTime.Today;
            var age = today.Year - DateOfBirth.Year;
            // Если ещё не было дня рождения в этом году
            if (DateOfBirth > today.AddYears(-age))
                age--;
            return age;
        }
    }

    /// <summary>
    /// Индекс массы тела (ИМТ)
    /// Формула: вес (кг) / (рост (м))^2
    /// </summary>
    public double BodyMassIndex => Weight / Math.Pow(Height / 100.0, 2);
}
