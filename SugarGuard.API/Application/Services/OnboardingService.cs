using Microsoft.EntityFrameworkCore;
using SugarGuard.API.Application.Interfaces;
using SugarGuard.API.Data;
using SugarGuard.Shared.Dto;
using SugarGuard.Domain.Entities;
using SugarGuard.Domain.Enums;

namespace SugarGuard.API.Application.Services;

/// <summary>
/// Реализация сервиса онбординга
/// </summary>

public sealed class OnboardingService : IOnboardingService
{
    private readonly AppDbContext _db;
    private readonly ILogger<OnboardingService> _logger;
    
    private const int TotalStepsParent = 5; // Количество шагов для роли Parent
    
    private const int TotalStepsDoctor = 4; // Количество шагов для роли Doctor
    
    private const int TotalStepsDefault = 3; // Количество шагов для всех остальных ролей

    /// <summary>
    /// Карта шагов. Используется при логировании событий.
    /// </summary>
    private static readonly IReadOnlyDictionary<int, string> StepNames =
        new Dictionary<int, string>
        {
            { 1, "welcome" },
            { 2, "email-verification" },
            { 3, "profile-setup" },
            { 4, "child-setup" },
            { 5, "first-measurement" }
        };

    public OnboardingService(
        AppDbContext db,
        ILogger<OnboardingService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<OnboardingStatusResponse> GetStatusAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "GetOnboardingStatus: пользователь {UserId} не найден.", userId);
            throw new KeyNotFoundException(
                $"Пользователь {userId} не найден.");
        }

        return await BuildStatusAsync(user, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OnboardingStatusResponse> CompleteStepAsync(
        Guid userId,
        int step,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "CompleteStep: пользователь {UserId} не найден.", userId);
            throw new KeyNotFoundException(
                $"Пользователь {userId} не найден.");
        }

        var totalSteps = GetTotalSteps(user.Role);

        if (step < 1 || step > totalSteps)
        {
            throw new ArgumentOutOfRangeException(
                nameof(step),
                $"Шаг {step} недопустим для роли {user.Role}. " +
                $"Допустимый диапазон: 1–{totalSteps}.");
        }

        // Не даём откатить прогресс назад — шаг обновляется только вперёд
        if (step > user.OnboardingCurrentStep)
        {
            user.OnboardingCurrentStep = step;
        }

        // Если шаг последний — автоматически завершаем онбординг
        if (step >= totalSteps && !user.OnboardingCompleted)
        {
            user.OnboardingCompleted = true;
            user.OnboardingCompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Пользователь {UserId} завершил онбординг (шаг {Step}/{Total}).",
                userId, step, totalSteps);
        }

        // Если это первый шаг — фиксируем начало онбординга
        if (step == 1 && user.OnboardingStartedAt is null)
        {
            user.OnboardingStartedAt = DateTime.UtcNow;
        }

        // Записываем событие
        var stepName = StepNames.TryGetValue(step, out var name)
            ? name
            : $"step-{step}";

        _db.OnboardingEvents.Add(new OnboardingEvent
        {
            OnboardingEventId = Guid.NewGuid(),
            UserId = userId,
            StepNumber = step,
            StepName = stepName,
            EventType = "completed",
            UserRole = user.Role.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Пользователь {UserId} завершил шаг {Step} онбординга ({StepName}).",
            userId, step, stepName);

        return await BuildStatusAsync(user, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SkipAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning(
                "SkipOnboarding: пользователь {UserId} не найден.", userId);
            throw new KeyNotFoundException(
                $"Пользователь {userId} не найден.");
        }

        // Если онбординг уже пройден — ничего не делаем
        if (user.OnboardingCompleted)
        {
            _logger.LogInformation(
                "SkipOnboarding: пользователь {UserId} уже завершил онбординг, пропуск игнорируется.",
                userId);
            return;
        }

        user.OnboardingCompleted = true;
        user.OnboardingSkippedAt = DateTime.UtcNow;
        user.OnboardingCompletedAt = DateTime.UtcNow;

        // Записываем событие 
        var currentStep = user.OnboardingCurrentStep < 1
            ? 1
            : user.OnboardingCurrentStep;

        var stepName = StepNames.TryGetValue(currentStep, out var name)
            ? name
            : $"step-{currentStep}";

        _db.OnboardingEvents.Add(new OnboardingEvent
        {
            OnboardingEventId = Guid.NewGuid(),
            UserId = userId,
            StepNumber = currentStep,
            StepName = stepName,
            EventType = "skipped",
            UserRole = user.Role.ToString(),
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Пользователь {UserId} пропустил онбординг на шаге {Step}.",
            userId, currentStep);
    }

    /// <inheritdoc/>
    public int GetTotalSteps(UserRole role) => role switch
    {
        UserRole.Parent => TotalStepsParent,
        UserRole.Doctor => TotalStepsDoctor,
        _ => TotalStepsDefault
    };

    /// <summary>
    /// Собирает инфу с актуальными данными о пользователе
    /// </summary>
    private async Task<OnboardingStatusResponse> BuildStatusAsync(
        User user,
        CancellationToken cancellationToken)
    {
        var totalSteps = GetTotalSteps(user.Role);

        // есть ли привязка к ребёнку.
        bool hasChild = false;
        Guid? childId = null;
        if (user.Role == UserRole.Parent)
        {
            var firstLink = await _db.ParentChildLinks
                .AsNoTracking()
                .Where(l => l.ParentUserId == user.UserId)
                .Select(l => l.ChildId)
                .FirstOrDefaultAsync(cancellationToken);
            hasChild = firstLink != Guid.Empty;
            childId = hasChild ? firstLink : null;
        }

        // настройки диабета заполнены хотя бы для одного ребёнка.
        bool hasDiabetesSettings = false;
        if (user.Role == UserRole.Parent && childId.HasValue)
        {
            hasDiabetesSettings = await _db.DiabetesSettings
                .AsNoTracking()
                .AnyAsync(d => d.ChildId == childId.Value, cancellationToken);
        }

        bool isApprovedByAdmin = user.Role switch
        {
            UserRole.Doctor => true,  // User с ролью Doctor = одобрен админом
            _ => true
        };

        var currentStepInt = user.OnboardingCurrentStep;
        var progressPercent = totalSteps > 0
            ? Math.Clamp(100 * currentStepInt / totalSteps, 0, 100)
            : 0;

        return new OnboardingStatusResponse
        {
            IsCompleted = user.OnboardingCompleted,
            CurrentStep = MapStepToOnboardingStep(currentStepInt, user.Role),
            Role = user.Role.ToString(),
            IsEmailVerified = user.IsEmailVerified,
            HasChild = hasChild,
            HasDiabetesSettings = hasDiabetesSettings,
            IsApprovedByAdmin = isApprovedByAdmin,
            ChildId = childId,
            ProgressPercent = progressPercent
        };
    }

    /// <summary>
    /// Маппинг внутреннего номера шага на строковую константу
    /// </summary>
    private static string MapStepToOnboardingStep(int step, UserRole role)
    {
        // 0 = не начат - клиент должен увидеть «подтвердите email».
        if (step <= 1)
            return OnboardingStep.EmailVerification;

        if (step == 2)
            return OnboardingStep.EmailVerification;

        if (step == 3 || step == 4)
        {
            // Для врача эти шаги означают ожидание одобрения админа,
            return role == UserRole.Doctor
                ? OnboardingStep.AwaitAdminApproval
                : OnboardingStep.CreateChild;
        }

        // 5+ = завершение
        return OnboardingStep.Done;
    }
}
