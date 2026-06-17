namespace SugarGuard.Junior.Views.Pages;

public partial class BackpackPage : SwipeablePage
{
    private readonly BackpackPageViewModel _viewModel;

    // Защита от повторного одновременного запуска инициализации.
    private bool _isInitializing;

    public BackpackPage(BackpackPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();

        // Если экран уже загружается, повторно инициализацию не запускаем.
        if (_isInitializing)
            return;

        _isInitializing = true;

        try
        {
            await _viewModel.InitializeAsync();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"BackpackPage OnAppearing error: {ex}");
            await DisplayAlert("Ошибка", $"Не удалось загрузить рюкзак: {ex.Message}", "ОК");
        }
        finally
        {
            _isInitializing = false;
        }
    }
}
