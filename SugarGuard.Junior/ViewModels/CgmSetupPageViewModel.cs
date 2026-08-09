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

    [ObservableProperty] private string statusText = "Выберите Juggluco, чтобы начать.";
    [ObservableProperty] private bool isConnected;

    public CgmSetupPageViewModel(
        IStorageService storage,
        ICgmConnectionService connection,
        ICgmBridgeLauncher launcher)
    {
        _storage = storage;
        _connection = connection;
        _launcher = launcher;
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
    private Task CloseAsync() => Shell.Current?.Navigation.PopModalAsync() ?? Task.CompletedTask;
}
