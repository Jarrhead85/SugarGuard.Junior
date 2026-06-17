namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Базовый класс для страниц с поддержкой свайпов для переключения вкладок.
/// Распознаёт горизонтальное движение пальца влево/вправо по всей области экрана
/// (при достаточном горизонтальном смещении и преобладании над вертикальным).
/// </summary>
public class SwipeablePage : ContentPage
{
    private const double MinSwipeDistance = 60;
    private const double HorizontalDominanceRatio = 2.0; // горизонталь должна быть в 2 раза больше вертикали

    protected override void OnAppearing()
    {
        base.OnAppearing();
        AttachGestureRecognizers();
    }

    private void AttachGestureRecognizers()
    {
        if (Content == null) return;

        // Если контент был обёрнут в старую сетку с полосами — возвращаем исходный контент
        if (Content is Grid grid && grid.ColumnDefinitions.Count == 3 && grid.Children.Count >= 1)
        {
            var mainContent = grid.Children.OfType<View>().FirstOrDefault(c => Grid.GetColumn(c) == 1);
            if (mainContent != null)
            {
                grid.Children.Remove(mainContent);
                Content = mainContent;
            }
        }

        if (Content == null) return;
        if (Content.GestureRecognizers.Any(g => g is PanGestureRecognizer))
            return;

        var pan = new PanGestureRecognizer();
        pan.PanUpdated += OnPanUpdated;
        Content.GestureRecognizers.Add(pan);
    }

    private async void OnPanUpdated(object? sender, PanUpdatedEventArgs e)
    {
        if (e.StatusType != GestureStatus.Completed)
            return;

        double totalX = e.TotalX;
        double totalY = e.TotalY;

        // Только явно горизонтальный жест: смещение по X достаточное и преобладает над Y
        if (Math.Abs(totalX) < MinSwipeDistance)
            return;
        if (Math.Abs(totalY) * HorizontalDominanceRatio > Math.Abs(totalX))
            return;

        if (Application.Current?.Windows.FirstOrDefault()?.Page is not AppShell shell)
            return;

        if (totalX > 0)
            await shell.NavigateToPreviousTab();
        else
            await shell.NavigateToNextTab();
    }
}
