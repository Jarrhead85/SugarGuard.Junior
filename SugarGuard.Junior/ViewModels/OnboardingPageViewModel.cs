using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Models.Core;
using SugarGuard.Junior.Repositories.Interfaces;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;
using SugarGuard.Shared.Dto;
using MobileDiabetesType = SugarGuard.Junior.Models.Enums.DiabetesType;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для онбординга после верификации.
/// 3 шага: имя/никнейм, тип диабета, целевой диапазон глюкозы.
/// </summary>
public partial class OnboardingPageViewModel : ObservableObject
{
    private readonly IStorageService _storageService;
    private readonly IApiClient _apiClient;
    private readonly ILogger<OnboardingPageViewModel> _logger;
    private readonly IChildRepository _childRepository;
    private readonly IChildSessionBootstrapService _childSessionBootstrapService;

    [ObservableProperty]
    private int currentStep;

    [ObservableProperty]
    private string nickName = string.Empty;

    [ObservableProperty]
    private int diabetesTypeIndex;

    [ObservableProperty]
    private double targetRangeMin = 4.0;

    [ObservableProperty]
    private double targetRangeMax = 10.0;

    [ObservableProperty]
    private bool canGoNext;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    public List<string> DiabetesTypes { get; } = new()
    {
        "Тип 1",
        "Тип 2",
        "Другой"
    };

    public OnboardingPageViewModel(
        IStorageService storageService,
        IApiClient apiClient,
        ILogger<OnboardingPageViewModel> logger,
        IChildRepository childRepository,
        IChildSessionBootstrapService childSessionBootstrapService)
    {
        _storageService = storageService;
        _apiClient = apiClient;
        _logger = logger;
        _childRepository = childRepository;
        _childSessionBootstrapService = childSessionBootstrapService;
        ValidateStep();
    }

    public bool IsStep0Active => CurrentStep >= 0;
    public bool IsStep1Active => CurrentStep >= 1;
    public bool IsStep2Active => CurrentStep >= 2;

    partial void OnCurrentStepChanged(int value)
    {
        OnPropertyChanged(nameof(IsStep0Active));
        OnPropertyChanged(nameof(IsStep1Active));
        OnPropertyChanged(nameof(IsStep2Active));
        ValidateStep();
    }

    partial void OnNickNameChanged(string value)
    {
        ValidateStep();
    }

    partial void OnDiabetesTypeIndexChanged(int value)
    {
        ValidateStep();
    }

    private void ValidateStep()
    {
        CanGoNext = CurrentStep switch
        {
            0 => !string.IsNullOrWhiteSpace(NickName),
            1 => DiabetesTypeIndex >= 0 && DiabetesTypeIndex < DiabetesTypes.Count,
            2 => TargetRangeMin < TargetRangeMax - 0.5,
            _ => false
        };
    }

    [RelayCommand]
    private void GoToNextStep()
    {
        if (CurrentStep < 2)
        {
            CurrentStep++;
            ValidateStep();
        }
    }

    [RelayCommand]
    private void GoToPreviousStep()
    {
        if (CurrentStep > 0)
        {
            CurrentStep--;
            ValidateStep();
        }
    }

    [RelayCommand]
    private async Task CompleteOnboardingAsync()
    {
        try
        {
            _logger.LogInformation(
                "Онбординг завершён: ник={NickName}, тип диабета={Type}, диапазон={Min}-{Max} ммоль/л",
                NickName, DiabetesTypes[DiabetesTypeIndex], TargetRangeMin, TargetRangeMax);

            var restoredExistingChild = await _childSessionBootstrapService.EnsureChildSessionAsync();
            if (restoredExistingChild)
            {
                _logger.LogInformation("Онбординг открыт повторно, но серверный профиль уже существует. Новый ребёнок не создаётся.");
                await Shell.Current.GoToAsync("//mainpage");
                return;
            }

            var firstName = await _storageService.GetAsync("pending_child_first_name");
            var lastName = await _storageService.GetAsync("pending_child_last_name");
            var dateOfBirthRaw = await _storageService.GetAsync("pending_child_date_of_birth");

            if (string.IsNullOrWhiteSpace(firstName))
            {
                firstName = NickName.Trim();
            }

            if (string.IsNullOrWhiteSpace(lastName))
            {
                lastName = "Ребенок";
            }

            var dateOfBirth = DateOnly.FromDateTime(DateTime.Today.AddYears(-10));
            if (DateTime.TryParse(dateOfBirthRaw, out var parsedDateOfBirth))
            {
                dateOfBirth = DateOnly.FromDateTime(parsedDateOfBirth);
            }

            var response = await _apiClient.CreateChildOnboardingAsync(new CreateChildOnboardingRequest
            {
                FirstName = firstName,
                LastName = lastName,
                DateOfBirth = dateOfBirth,
                DiabetesType = MapDiabetesType(DiabetesTypeIndex),
                TimeZoneId = TimeZoneInfo.Local.Id,
                TargetRangeMin = Convert.ToDecimal(TargetRangeMin),
                TargetRangeMax = Convert.ToDecimal(TargetRangeMax)
            });

            if (!response.Success || response.ChildId is null)
            {
                ErrorMessage = response.ErrorMessage
                    ?? "Не удалось создать профиль ребенка. Попробуйте еще раз.";
                return;
            }

            var childId = response.ChildId.Value.ToString();
            await SaveLocalChildProfileAsync(childId, firstName, lastName, dateOfBirth);

            await _storageService.SaveAsync(Constants.StorageKeyCurrentChildId, childId);
            await _storageService.SaveAsync("onboarding_completed", "true");
            await _storageService.SaveAsync("child_nickname", NickName);
            await _storageService.SaveAsync("diabetes_type", DiabetesTypeIndex.ToString());
            await _storageService.SaveAsync("target_range_min", TargetRangeMin.ToString("F1"));
            await _storageService.SaveAsync("target_range_max", TargetRangeMax.ToString("F1"));

            await Shell.Current.GoToAsync("//mainpage");
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось сохранить настройки. Попробуйте позже.";
            _logger.LogError(ex, "Ошибка при завершении онбординга");
        }
    }

    private static string MapDiabetesType(int index) => index switch
    {
        0 => DiabetesType.Type1,
        1 => DiabetesType.Type2,
        _ => DiabetesType.Other
    };

    private async Task SaveLocalChildProfileAsync(
        string childId,
        string firstName,
        string lastName,
        DateOnly dateOfBirth)
    {
        var existing = await _childRepository.GetByIdAsync(childId);
        var now = DateTime.UtcNow;
        var localDiabetesType = DiabetesTypeIndex switch
        {
            0 => MobileDiabetesType.Type1,
            1 => MobileDiabetesType.Type2,
            _ => MobileDiabetesType.Other
        };

        var child = new Child
        {
            ChildId = childId,
            ParentUserId = await _storageService.GetAsync(Constants.StorageKeyCurrentUserId) ?? "self",
            EncryptedFirstName = firstName.Trim(),
            EncryptedLastName = lastName.Trim(),
            DateOfBirth = dateOfBirth.ToDateTime(TimeOnly.MinValue),
            Weight = 30,
            Height = 130,
            DiabetesType = localDiabetesType,
            DiagnosisDate = DateTime.Today,
            InsulinScheme = string.Empty,
            CurrentInsulins = "[]",
            CreatedAt = existing?.CreatedAt ?? now,
            UpdatedAt = now,
            PhotoUrl = existing?.PhotoUrl
        };

        if (existing is null)
        {
            await _childRepository.AddChildWithEncryptionAsync(child);
        }
        else
        {
            await _childRepository.UpdateChildWithEncryptionAsync(child);
        }
    }

    [RelayCommand]
    private async Task SkipOnboardingAsync()
    {
        _logger.LogInformation("Онбординг пропущен пользователем");
        await _storageService.SaveAsync("onboarding_completed", "false");
        await Shell.Current.GoToAsync("//mainpage");
    }
}
