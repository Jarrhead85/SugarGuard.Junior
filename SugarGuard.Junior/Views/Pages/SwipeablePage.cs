namespace SugarGuard.Junior.Views.Pages;

/// <summary>
/// Базовый класс для страниц с поддержкой свайпов для переключения вкладок.
/// Распознаёт горизонтальный свайп влево/вправо по всей области экрана.
/// Использует SwipeGestureRecognizer, чтобы не перехватывать вертикальный скролл.
/// </summary>
public class SwipeablePage : ContentPage
{
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

        if (Content is ScrollView or CollectionView)
            return;

        if (Content.GestureRecognizers.Any(g => g is SwipeGestureRecognizer))
            return;

        var leftSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Left };
        leftSwipe.Swiped += OnSwiped;
        Content.GestureRecognizers.Add(leftSwipe);

        var rightSwipe = new SwipeGestureRecognizer { Direction = SwipeDirection.Right };
        rightSwipe.Swiped += OnSwiped;
        Content.GestureRecognizers.Add(rightSwipe);
    }

    private static async void OnSwiped(object? sender, SwipedEventArgs e)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is AppShell shell)
        {
            if (e.Direction == SwipeDirection.Right)
                await shell.NavigateToPreviousTab();
            else if (e.Direction == SwipeDirection.Left)
                await shell.NavigateToNextTab();
        }
    }
}
