using SugarGuard.Junior.Views.Pages;

namespace SugarGuard.Junior;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();

        // ShellContent routes from AppShell.xaml are already registered by Shell.
        // Register only extra pages to avoid ambiguous route matches on Android.
        Routing.RegisterRoute("chartpage", typeof(ChartPage));
        Routing.RegisterRoute("schedulepage", typeof(SchedulePage));
        Routing.RegisterRoute("recommendation-modal", typeof(RecommendationModal));
        Routing.RegisterRoute("helpalertpage", typeof(HelpAlertPage));
    }

    /// <summary>
    /// Navigates to the next main tab.
    /// </summary>
    public async Task NavigateToNextTab()
    {
        var currentRoute = CurrentState.Location.ToString().Split('/').LastOrDefault();
        var routes = new[] { "mainpage", "historypage", "backpackpage", "profilepage" };
        var currentIndex = Array.IndexOf(routes, currentRoute);

        if (currentIndex >= 0 && currentIndex < routes.Length - 1)
        {
            await GoToAsync($"///{routes[currentIndex + 1]}");
        }
    }

    /// <summary>
    /// Navigates to the previous main tab.
    /// </summary>
    public async Task NavigateToPreviousTab()
    {
        var currentRoute = CurrentState.Location.ToString().Split('/').LastOrDefault();
        var routes = new[] { "mainpage", "historypage", "backpackpage", "profilepage" };
        var currentIndex = Array.IndexOf(routes, currentRoute);

        if (currentIndex > 0)
        {
            await GoToAsync($"///{routes[currentIndex - 1]}");
        }
    }
}
