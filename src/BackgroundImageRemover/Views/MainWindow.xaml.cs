using System.ComponentModel;
using System.Windows;
using BackgroundImageRemover.ViewModels;

namespace BackgroundImageRemover.Views;

public partial class MainWindow : Window
{
    public MainWindow(ShellViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void MainWindow_Closing(object? sender, CancelEventArgs e)
    {
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
