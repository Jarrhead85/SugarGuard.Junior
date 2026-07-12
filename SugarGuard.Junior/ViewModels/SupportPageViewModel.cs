using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SugarGuard.Domain.Enums;
using SugarGuard.Junior.Models.Api;
using SugarGuard.Junior.Services.Interfaces;

namespace SugarGuard.Junior.ViewModels;

public partial class SupportPageViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;
    private readonly ILogger<SupportPageViewModel> _logger;

    [ObservableProperty]
    private bool isBusy;

    [ObservableProperty]
    private string subject = string.Empty;

    [ObservableProperty]
    private string newMessage = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSelectedConversation))]
    private SupportConversationApiModel? selectedConversation;

    [ObservableProperty]
    private string statusMessage = string.Empty;

    public ObservableCollection<SupportConversationApiModel> Conversations { get; } = new();
    public ObservableCollection<SupportMessageApiModel> Messages { get; } = new();

    public bool HasSelectedConversation => SelectedConversation is not null;

    public SupportPageViewModel(IApiClient apiClient, ILogger<SupportPageViewModel> logger)
    {
        _apiClient = apiClient;
        _logger = logger;
    }

    partial void OnSelectedConversationChanged(SupportConversationApiModel? value)
    {
        if (value is not null)
        {
            _ = LoadConversationAsync(value.ConversationId);
        }
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        if (IsBusy)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            Conversations.Clear();

            var conversations = await _apiClient.GetSupportConversationsAsync();
            foreach (var item in conversations.OrderByDescending(item => item.UpdatedAt))
            {
                Conversations.Add(item);
            }

            if (SelectedConversation is null && Conversations.Count > 0)
            {
                SelectedConversation = Conversations[0];
            }

            if (Conversations.Count == 0)
            {
                Messages.Clear();
                StatusMessage = "Пока нет обращений. Напиши нам, если нужна помощь.";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load support conversations");
            StatusMessage = "Не удалось загрузить обращения. Проверь интернет и попробуй еще раз.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task CreateAsync()
    {
        var trimmedSubject = Subject.Trim();
        var trimmedMessage = NewMessage.Trim();
        if (trimmedSubject.Length < 3 || trimmedMessage.Length < 2)
        {
            StatusMessage = "Добавь короткую тему и сообщение.";
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var created = await _apiClient.CreateSupportConversationAsync(new CreateSupportConversationApiRequest
            {
                Subject = trimmedSubject,
                Message = trimmedMessage
            });

            if (created is null)
            {
                StatusMessage = "Не удалось отправить обращение. Попробуй еще раз.";
                return;
            }

            Subject = string.Empty;
            NewMessage = string.Empty;
            Conversations.Insert(0, created);
            SelectedConversation = created;
            Messages.Clear();
            foreach (var message in created.Messages.OrderBy(item => item.CreatedAt))
            {
                Messages.Add(message);
            }

            StatusMessage = "Обращение отправлено в поддержку.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create support conversation");
            StatusMessage = "Не удалось отправить обращение. Попробуй позже.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        if (SelectedConversation is null)
        {
            StatusMessage = "Выбери обращение или создай новое.";
            return;
        }

        var trimmedMessage = NewMessage.Trim();
        if (trimmedMessage.Length == 0)
        {
            return;
        }

        try
        {
            IsBusy = true;
            StatusMessage = string.Empty;
            var sent = await _apiClient.AddSupportMessageAsync(SelectedConversation.ConversationId, trimmedMessage);
            if (sent is null)
            {
                StatusMessage = "Не удалось отправить сообщение.";
                return;
            }

            NewMessage = string.Empty;
            Messages.Add(sent);
            await _apiClient.MarkSupportConversationReadAsync(SelectedConversation.ConversationId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send support message");
            StatusMessage = "Сообщение не отправлено. Проверь соединение.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadConversationAsync(Guid conversationId)
    {
        try
        {
            var details = await _apiClient.GetSupportConversationAsync(conversationId);
            if (details is null)
            {
                return;
            }

            Messages.Clear();
            foreach (var message in details.Messages.OrderBy(item => item.CreatedAt))
            {
                Messages.Add(message);
            }

            await _apiClient.MarkSupportConversationReadAsync(conversationId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load support conversation {ConversationId}", conversationId);
            StatusMessage = "Не удалось открыть переписку.";
        }
    }

    public static string FormatStatus(SupportConversationStatus status) => status switch
    {
        SupportConversationStatus.WaitingForSupport => "Ждет поддержки",
        SupportConversationStatus.WaitingForUser => "Есть ответ",
        SupportConversationStatus.Closed => "Закрыто",
        _ => "Открыто"
    };
}
