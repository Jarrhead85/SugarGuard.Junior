using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Shared.Dto;
using AppConstants = SugarGuard.Junior.Utilities.Constants;

namespace SugarGuard.Junior.ViewModels;

/// <summary>
/// ViewModel для экрана управления связками ребёнка с родителями и врачами.
/// </summary>
public partial class AccessManagementPageViewModel : ObservableObject
{
    private readonly ILinkService _linkService;
    private readonly IStorageService _storageService;
    private readonly ILogger<AccessManagementPageViewModel> _logger;

    [ObservableProperty]
    private List<ParentChildLinkDto> parentLinks = new();

    [ObservableProperty]
    private List<DoctorChildLinkDto> doctorLinks = new();

    [ObservableProperty]
    private List<InviteCodeSummaryDto> incomingRequests = new();

    [ObservableProperty]
    private string inviteCode = string.Empty;

    [ObservableProperty]
    private DateTime inviteCodeExpiresAt;

    [ObservableProperty]
    private bool showInviteCode;

    [ObservableProperty]
    private string inviteCodeTargetRole = "Parent";

    [ObservableProperty]
    private bool isLoading;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private bool hasIncomingRequests;

    public AccessManagementPageViewModel(
        ILinkService linkService,
        IStorageService storageService,
        ILogger<AccessManagementPageViewModel> logger)
    {
        _linkService = linkService;
        _storageService = storageService;
        _logger = logger;
    }

    /// <summary>Читает childId из SecureStorage; бросает исключение если не задан.</summary>
    private async Task<Guid> GetCurrentChildIdAsync()
    {
        var raw = await _storageService.GetAsync(AppConstants.StorageKeyCurrentChildId);

        if (string.IsNullOrWhiteSpace(raw) || !Guid.TryParse(raw, out var childId))
        {
            throw new InvalidOperationException("Ребёнок не выбран. Перейдите на главный экран.");
        }

        return childId;
    }

    [RelayCommand]
    private async Task LoadAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;

            var childId = await GetCurrentChildIdAsync();

            var parents = _linkService.GetParentLinksAsync(childId);
            var doctors = _linkService.GetDoctorLinksAsync(childId);
            var incoming = _linkService.GetIncomingRequestsAsync();

            await Task.WhenAll(parents, doctors, incoming);

            ParentLinks = await parents;
            DoctorLinks = await doctors;
            IncomingRequests = await incoming;
            HasIncomingRequests = IncomingRequests.Count > 0;

            _logger.LogInformation("Загружено связок: {ParentCount} родителей, {DoctorCount} врачей, {IncomingCount} входящих",
                ParentLinks.Count, DoctorLinks.Count, IncomingRequests.Count);
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Не удалось загрузить данные";
            _logger.LogError(ex, "Ошибка загрузки списка связок");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateParentInviteAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            ShowInviteCode = false;

            var childId = await GetCurrentChildIdAsync();
            var result = await _linkService.GenerateParentInviteCodeAsync(childId);

            if (!string.IsNullOrEmpty(result.DisplayCode))
            {
                InviteCode = result.DisplayCode;
                InviteCodeExpiresAt = result.ExpiresAt;
                InviteCodeTargetRole = "Parent";
                ShowInviteCode = true;

                _logger.LogInformation("Сгенерирован код приглашения для родителя: {Code}", result.DisplayCode);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось сгенерировать код";
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка генерации кода";
            _logger.LogError(ex, "Ошибка генерации кода приглашения для родителя");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task GenerateDoctorInviteAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            IsLoading = true;
            ErrorMessage = string.Empty;
            ShowInviteCode = false;

            var childId = await GetCurrentChildIdAsync();
            var result = await _linkService.GenerateDoctorInviteCodeAsync(childId);

            if (!string.IsNullOrEmpty(result.DisplayCode))
            {
                InviteCode = result.DisplayCode;
                InviteCodeExpiresAt = result.ExpiresAt;
                InviteCodeTargetRole = "Doctor";
                ShowInviteCode = true;

                _logger.LogInformation("Сгенерирован код приглашения для врача: {Code}", result.DisplayCode);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось сгенерировать код";
            }
        }
        catch (InvalidOperationException ex)
        {
            ErrorMessage = ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка генерации кода";
            _logger.LogError(ex, "Ошибка генерации кода приглашения для врача");
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RemoveParentLinkAsync(Guid linkId)
    {
        try
        {
            var result = await _linkService.RemoveParentLinkAsync(linkId);

            if (result.Success)
            {
                ParentLinks = ParentLinks.Where(l => l.LinkId != linkId).ToList();
                _logger.LogInformation("Связь с родителем {LinkId} удалена", linkId);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось удалить связь";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка удаления связи";
            _logger.LogError(ex, "Ошибка удаления связи с родителем");
        }
    }

    [RelayCommand]
    private async Task RemoveDoctorLinkAsync(Guid linkId)
    {
        try
        {
            var result = await _linkService.RemoveDoctorLinkAsync(linkId);

            if (result.Success)
            {
                DoctorLinks = DoctorLinks.Where(l => l.LinkId != linkId).ToList();
                _logger.LogInformation("Связь с врачом {LinkId} удалена", linkId);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось удалить связь";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка удаления связи";
            _logger.LogError(ex, "Ошибка удаления связи с врачом");
        }
    }

    [RelayCommand]
    private async Task ApproveIncomingRequestAsync(Guid inviteCodeId)
    {
        try
        {
            var result = await _linkService.ApproveLinkRequestAsync(inviteCodeId);

            if (result.Success)
            {
                IncomingRequests = IncomingRequests.Where(r => r.InviteCodeId != inviteCodeId).ToList();
                HasIncomingRequests = IncomingRequests.Count > 0;
                _logger.LogInformation("Входящий запрос {InviteCodeId} подтверждён", inviteCodeId);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось подтвердить запрос";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка подтверждения запроса";
            _logger.LogError(ex, "Ошибка подтверждения входящего запроса");
        }
    }

    [RelayCommand]
    private async Task RejectIncomingRequestAsync(Guid inviteCodeId)
    {
        try
        {
            var result = await _linkService.RejectLinkRequestAsync(inviteCodeId);

            if (result.Success)
            {
                IncomingRequests = IncomingRequests.Where(r => r.InviteCodeId != inviteCodeId).ToList();
                HasIncomingRequests = IncomingRequests.Count > 0;
                _logger.LogInformation("Входящий запрос {InviteCodeId} отклонён", inviteCodeId);
            }
            else
            {
                ErrorMessage = result.ErrorMessage ?? "Не удалось отклонить запрос";
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = "Ошибка отклонения запроса";
            _logger.LogError(ex, "Ошибка отклонения входящего запроса");
        }
    }

    [RelayCommand]
    private void CopyInviteCode()
    {
        if (!string.IsNullOrEmpty(InviteCode))
        {
            Clipboard.Default.SetTextAsync(InviteCode);
            _logger.LogInformation("Код приглашения скопирован в буфер обмена");
        }
    }

    [RelayCommand]
    private void CloseInviteCode()
    {
        ShowInviteCode = false;
        InviteCode = string.Empty;
    }
}
