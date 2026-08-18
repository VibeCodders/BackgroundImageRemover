using System.Windows;
using System.Windows.Input;
using BackgroundImageRemover.Models;

namespace BackgroundImageRemover.Views;

public partial class NewProjectDialog : Window
{
    public NewProjectType? SelectedType { get; private set; }
    public bool OpenImageImmediately => PickImageCheckBox.IsChecked == true;

    public NewProjectDialog()
    {
        InitializeComponent();
    }

    private void BackgroundRemoverCard_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedType = NewProjectType.BackgroundRemover;
        DialogResult = true;
    }

    private void BackgroundRemoverButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = NewProjectType.BackgroundRemover;
        DialogResult = true;
    }

    private void UncropCard_Click(object sender, MouseButtonEventArgs e)
    {
        SelectedType = NewProjectType.Uncrop;
        DialogResult = true;
    }

    private void UncropButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = NewProjectType.Uncrop;
        DialogResult = true;
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = null;
        DialogResult = false;
    }
}
