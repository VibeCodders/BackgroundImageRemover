using System.Reflection;
using System.Windows;

namespace BackgroundImageRemover.Views;

/// <summary>Small About dialog showing the application name and version.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();

        var version = Assembly.GetExecutingAssembly().GetName().Version;
        VersionText.Text = version is not null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version unknown";
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();
}
