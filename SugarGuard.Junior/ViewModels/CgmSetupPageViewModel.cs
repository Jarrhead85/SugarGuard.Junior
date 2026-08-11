using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SugarGuard.Junior.Services.Interfaces;
using SugarGuard.Junior.Utilities;

namespace SugarGuard.Junior.ViewModels;

/// <summary>Пошаговая настройка локальной передачи CGM без учётных данных производителя.</summary>
public partial class CgmSetupPageViewModel : ObservableObject
{
    private readonly IStorageService _storage;
    private readonly ICgmConnectionService _connection;
    private readonly ICgmBridgeLauncher _launcher;
    private readonly ISensorGlucoseIngestionService _ingestion;

    [ObservableProperty] private string statusText = "Выберите Juggluco, чтобы начать.";
    [ObservableProperty] private bool isConnected;
    [ObservableProperty] private bool hasPendingReading;
    [ObservableProperty] private string pendingReadingText = string.Empty;
    private string? _pendingConfirmationId;

    public CgmSetupPageViewModel(
        IStorageService storage,
        ICgmConnectionService connection,
        ICgmBridgeLauncher launcher,
        ISensorGlucoseIngestionService ingestion)
    {
        _storage = storage;
        _connection = connection;
        _launcher = launcher;
        _ingestion = ingestion;
    }

    public async Task InitializeAsync()
    {
        var childId = await _storage.GetAsync(Constants.StorageKeyCurrentChildId);
        var status = await _connection.GetStatusAsync();
        IsConnected = status.IsConnected && string.Equals(status.ChildId, childId, StringComparison.Ordinal);
        StatusText = IsConnected
            ? status.LastReadingAtUtc is { } last
                ? $"Последние данные: {last.ToLocalTime():dd.MM HH:mm}."
                : "Связка сохранена. Ожидаем первое показание из Juggluco."
            : "Датчик подключается через приложение на этом же телефоне.";

        await LoadPendingReadingAsync();
    }

    [RelayCommand]
    private async Task SelectJugglucoAsync()
    {
        var childId = await _storage.GetAsync(Constants.StorageKeyCurrentChildId);
        if (string.IsNullOrWhiteSpace(childId))
        {
            StatusText = "Сначала выберите профиль ребёнка на главном экране.";
            return;
        }

        await _connection.ConnectAsync(childId, "Juggluco");
        IsConnected = true;
        StatusText = "Juggluco выбран. Откройте его и включите передачу на шаге 2.";
    }

    [RelayCommand]
    private async Task OpenJugglucoAsync()
    {
        if (!await _launcher.OpenAsync("Juggluco"))
        {
            StatusText = "Juggluco не найден. Установите его и вернитесь в SugarGuard.";
            return;
        }

        StatusText = "В Juggluco: Settings → Exchange data → Glucodata/xDrip broadcast → SugarGuard.";
    }

    [RelayCommand]
    private async Task DisconnectAsync()
    {
        await _connection.DisconnectAsync();
        IsConnected = false;
        StatusText = "Передача CGM отключена.";
    }

    [RelayCommand]
    private async Task ConfirmPendingReadingAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingConfirmationId)) return;

        var result = await _ingestion.ConfirmPendingAsync(_pendingConfirmationId);
        StatusText = result.IsSaved
            ? "Показание подтверждено и добавлено в историю."
            : result.IsDuplicate
                ? "Это показание уже есть в истории."
                : result.ErrorMessage ?? "Не удалось подтвердить показание.";
        await LoadPendingReadingAsync();
    }

    [RelayCommand]
    private async Task RejectPendingReadingAsync()
    {
        if (string.IsNullOrWhiteSpace(_pendingConfirmationId)) return;

        await _ingestion.RejectPendingAsync(_pendingConfirmationId);
        StatusText = "Неподтверждённое показание отклонено.";
        await LoadPendingReadingAsync();
    }

    [RelayCommand]
    private Task RefreshPendingReadingAsync() => LoadPendingReadingAsync();

    private async Task LoadPendingReadingAsync()
    {
        var pending = await _ingestion.GetPendingAsync();
        _pendingConfirmationId = pending?.ConfirmationId;
        HasPendingReading = pending is not null;
        PendingReadingText = pending is null
            ? string.Empty
            : $"{pending.Reading.GlucoseMmolPerLiter:F1} ммоль/л · {pending.Reading.MeasurementTimeUtc.ToLocalTime():HH:mm}";
    }

    [RelayCommand]
    private Task CloseAsync() => Shell.Current?.Navigation.PopModalAsync() ?? Task.CompletedTask;
}
