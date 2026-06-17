using SugarGuard.Junior.ViewModels;

namespace SugarGuard.Junior.Views.Pages;

public partial class VerifyPage : ContentPage, IQueryAttributable
{
    private readonly VerifyPageViewModel _viewModel;

    public VerifyPage(VerifyPageViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        codeInput.CodeCompleted += OnCodeCompleted;
    }

    /// <summary>
    /// Shell передаёт query-параметры только странице, не ViewModel — пробрасываем вручную.
    /// </summary>
    public void ApplyQueryAttributes(IDictionary<string, object> query)
    {
        _viewModel.ApplyQueryAttributes(query);
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        codeInput.Reset();
    }

    private async void OnCodeCompleted(object? sender, EventArgs e)
    {
        // Синхронно копируем код из контрола: биндинг может не успеть обновить VM до команды.
        _viewModel.Code = codeInput.Code;
        await _viewModel.VerifyCodeCommand.ExecuteAsync(null);
    }
}
