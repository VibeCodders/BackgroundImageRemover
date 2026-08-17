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

    private void RecentMenu_SubmenuOpened(object sender, RoutedEventArgs e) => RefreshRecentMenus();

    private void WorkInProgressMenu_SubmenuOpened(object sender, RoutedEventArgs e) => RefreshRecentMenus();

    private void RefreshRecentMenus()
    {
        if (DataContext is ShellViewModel viewModel)
        {
            viewModel.RefreshRecentFiles();
            viewModel.RefreshRecentWorkFiles();
        }
    }
}
