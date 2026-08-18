using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace BackgroundImageRemover.Views;

/// <summary>Small About dialog showing the application name and version.</summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + DisplayVersion;
    }

    /// <summary>
    /// The real product version from the <c>&lt;Version&gt;</c> property (e.g. 1.22.0). The
    /// assembly version stays pinned and is not bumped with each release, so reading it here
    /// would show a stale number.
    /// </summary>
    internal static string DisplayVersion
    {
        get
        {
            var informational = Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion;
            // Strip the "+buildHash" suffix SourceLink appends to the informational version.
            var clean = informational?.Split('+')[0];
            return string.IsNullOrWhiteSpace(clean) ? "unknown" : clean;
        }
    }

    private void Ok_Click(object sender, RoutedEventArgs e) => Close();

    /// <summary>Opens the project page in the default browser.</summary>
    private void GitHubLink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
        }
        catch
        {
            // Opening a browser must never crash the About dialog.
        }
        e.Handled = true;
    }
}
