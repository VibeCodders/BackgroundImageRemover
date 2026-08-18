using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using BackgroundImageRemover.Services.Settings;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class MainWindow : Window
{
    private readonly ISettingsService _settings;

    public MainWindow(ShellViewModel viewModel, ISettingsService settings)
    {
        InitializeComponent();
        DataContext = viewModel;
        _settings = settings;
        Loaded += MainWindow_Loaded;
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        var state = _settings.Current;
        if (state.WindowWidth is not { } w || state.WindowHeight is not { } h || w < 200 || h < 200)
        {
            return;
        }

        // Keep the window reachable if the saved position drifted off-screen (monitor removed).
        double left = state.WindowLeft ?? SystemParameters.VirtualScreenLeft;
        double top = state.WindowTop ?? SystemParameters.VirtualScreenTop;
        double virtualRight = SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth;
        double virtualBottom = SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight;
        if (left < SystemParameters.VirtualScreenLeft - w + 80 || left + 80 > virtualRight)
        {
            left = SystemParameters.VirtualScreenLeft + 40;
        }
        if (top < SystemParameters.VirtualScreenTop - h + 40 || top + 40 > virtualBottom)
        {
            top = SystemParameters.VirtualScreenTop + 40;
        }

        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        Width = w;
        Height = h;
        if (state.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
        // Persist the geometry (Left/Top/Width/Height keep the restore bounds while maximized).
        var state = _settings.Current;
        state.WindowLeft = Left;
        state.WindowTop = Top;
        state.WindowWidth = Width;
        state.WindowHeight = Height;
        state.WindowMaximized = WindowState == WindowState.Maximized;
        _settings.Save();

        if (DataContext is ShellViewModel viewModel)
        {
            if (!await viewModel.ConfirmCloseAllAsync())
            {
                e.Cancel = true;
            }
        }
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        new AboutWindow { Owner = this }.ShowDialog();
    }

    /// <summary>Middle-clicking a tab header closes that tab, like most browsers/editors.</summary>
    private void TabHeader_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle)
        {
            return;
        }

        if (sender is FrameworkElement { DataContext: IDocumentTab tab } && DataContext is ShellViewModel shell)
        {
            shell.CloseTabCommand.Execute(tab);
            e.Handled = true;
        }
    }

    private void RecentMenu_SubmenuOpened(object sender, RoutedEventArgs e) => RefreshRecentMenus();

    private void RecentProjectsMenu_SubmenuOpened(object sender, RoutedEventArgs e) => RefreshRecentMenus();

    private void RefreshRecentMenus()
    {
        if (DataContext is ShellViewModel viewModel)
        {
            viewModel.RefreshRecentFiles();
            viewModel.RefreshRecentProjects();
        }
    }
}
