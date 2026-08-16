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
}
