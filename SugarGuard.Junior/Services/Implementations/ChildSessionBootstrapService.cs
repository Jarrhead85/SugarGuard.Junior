using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using MobileDiabetesType = SugarGuard.Junior.Models.Enums.DiabetesType;

namespace SugarGuard.Junior.Services.Implementations;

/// <summary>
/// Восстанавливает локальный childId после обновления приложения или потери служебных ключей.
/// </summary>
public sealed class ChildSessionBootstrapService : IChildSessionBootstrapService
{
    private const double DefaultWeightKg = 30;
    private const double DefaultHeightCm = 130;
    private const string OnboardingCompletedKey = "onboarding_completed";
    private const string ChildNicknameKey = "child_nickname";
    private const string DiabetesTypeKey = "diabetes_type";

    private readonly IApiClient _apiClient;
    private readonly IStorageService _storageService;
    private readonly IChildRepository _childRepository;
    private readonly IMeasurementRepository _measurementRepository;
    private readonly ILogger<ChildSessionBootstrapService> _logger;

    public ChildSessionBootstrapService(
        IApiClient apiClient,
        IStorageService storageService,
        IChildRepository childRepository,
        IMeasurementRepository measurementRepository,
        ILogger<ChildSessionBootstrapService> logger)
    {
        _apiClient = apiClient;
        _storageService = storageService;
        _childRepository = childRepository;
        _measurementRepository = measurementRepository;
        _logger = logger;
    }

    public async Task<bool> EnsureChildSessionAsync(CancellationToken cancellationToken = default)
    {
        var currentChildId = await _storageService.GetAsync(Constants.StorageKeyCurrentChildId);
        Child? localChild = null;
        var currentChildHasLocalMeasurements = false;

        if (!string.IsNullOrWhiteSpace(currentChildId))
        {
            localChild = await _childRepository.GetByIdAsync(currentChildId);
            currentChildHasLocalMeasurements = localChild is not null &&
                                               await _measurementRepository.GetLatestByChildIdAsync(currentChildId) is not null;
        }

        var serverChildren = await _apiClient.GetAccessibleChildrenAsync(cancellationToken);
        if (serverChildren.Count == 0)
        {
            if (localChild is not null && !string.IsNullOrWhiteSpace(currentChildId))
            {
                await MarkOnboardingCompletedAsync(localChild, currentChildId);
                return true;
            }

            _logger.LogInformation("На сервере не найдено профилей ребёнка для текущего аккаунта.");
            return false;
        }

        var selectedChild = SelectChild(serverChildren, currentChildId, currentChildHasLocalMeasurements);
        await SaveLocalChildProfileAsync(selectedChild);

        var selectedChildId = selectedChild.ChildId.ToString();
        await _storageService.SaveAsync(Constants.StorageKeyCurrentChildId, selectedChildId);
        await _storageService.SaveAsync(OnboardingCompletedKey, "true");
        await _storageService.SaveAsync(ChildNicknameKey, selectedChild.FirstName);
        await _storageService.SaveAsync(DiabetesTypeKey, MapDiabetesTypeIndex(selectedChild.DiabetesType).ToString());

        _logger.LogInformation(
            "Восстановлен локальный профиль ребёнка после обновления. ChildId={ChildId}",
            selectedChildId);

        return true;
    }

    private static ChildSummaryApiModel SelectChild(
        IReadOnlyCollection<ChildSummaryApiModel> serverChildren,
        string? preferredChildId,
        bool preferredChildHasLocalMeasurements)
    {
        if (!string.IsNullOrWhiteSpace(preferredChildId) &&
            Guid.TryParse(preferredChildId, out var parsedPreferredId))
        {
            var preferred = serverChildren.FirstOrDefault(c => c.ChildId == parsedPreferredId);
            if (preferred is not null && (serverChildren.Count == 1 || preferredChildHasLocalMeasurements))
                return preferred;
        }

        return serverChildren
            .OrderBy(c => c.CreatedAt == default ? DateTime.MaxValue : c.CreatedAt)
            .ThenBy(c => c.LastName, StringComparer.CurrentCulture)
            .ThenBy(c => c.FirstName, StringComparer.CurrentCulture)
            .First();
    }

    private async Task SaveLocalChildProfileAsync(ChildSummaryApiModel serverChild)
    {
        var childId = serverChild.ChildId.ToString();
        var existing = await _childRepository.GetByIdAsync(childId);
        var now = DateTime.UtcNow;

        var child = new Child
        {
            ChildId = childId,
            ParentUserId = await _storageService.GetAsync(Constants.StorageKeyCurrentUserId) ?? "self",
            EncryptedFirstName = string.IsNullOrWhiteSpace(serverChild.FirstName)
                ? "Ребёнок"
                : serverChild.FirstName.Trim(),
            EncryptedLastName = string.IsNullOrWhiteSpace(serverChild.LastName)
                ? "SugarGuard"
                : serverChild.LastName.Trim(),
            DateOfBirth = serverChild.DateOfBirth.ToDateTime(TimeOnly.MinValue),
            Weight = existing?.Weight > 0 ? existing.Weight : DefaultWeightKg,
            Height = existing?.Height > 0 ? existing.Height : DefaultHeightCm,
            DiabetesType = MapDiabetesType(serverChild.DiabetesType),
            DiagnosisDate = serverChild.DiagnosisDate?.ToDateTime(TimeOnly.MinValue)
                ?? existing?.DiagnosisDate
                ?? DateTime.Today,
            InsulinScheme = existing?.InsulinScheme ?? string.Empty,
            CurrentInsulins = existing?.CurrentInsulins ?? "[]",
            PhotoUrl = existing?.PhotoUrl,
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now
        };

        if (existing is null)
            await _childRepository.AddChildWithEncryptionAsync(child);
        else
            await _childRepository.UpdateChildWithEncryptionAsync(child);
    }

    private async Task MarkOnboardingCompletedAsync(Child localChild, string childId)
    {
        await _storageService.SaveAsync(Constants.StorageKeyCurrentChildId, childId);
        await _storageService.SaveAsync(OnboardingCompletedKey, "true");
        await _storageService.SaveAsync(ChildNicknameKey, await _childRepository.GetFirstNameAsync(localChild));
        await _storageService.SaveAsync(DiabetesTypeKey, MapDiabetesTypeIndex(localChild.DiabetesType.ToString()).ToString());
    }

    private static MobileDiabetesType MapDiabetesType(string? diabetesType)
    {
        if (string.IsNullOrWhiteSpace(diabetesType))
            return MobileDiabetesType.Type1;

        return diabetesType.Trim().ToLowerInvariant() switch
        {
            "type2" or "тип 2" or "2" => MobileDiabetesType.Type2,
            "lada" => MobileDiabetesType.LADA,
            "other" or "другой" => MobileDiabetesType.Other,
            _ => MobileDiabetesType.Type1
        };
    }

    private static int MapDiabetesTypeIndex(string? diabetesType)
    {
        return MapDiabetesType(diabetesType) switch
        {
            MobileDiabetesType.Type1 => 0,
            MobileDiabetesType.Type2 => 1,
            _ => 2
        };
    }
}
