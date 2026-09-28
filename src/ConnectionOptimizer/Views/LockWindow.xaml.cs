using System.Runtime.InteropServices;
using System.Windows;
using ConnectionOptimizer.Services.Licensing;
using Microsoft.Win32;

namespace ConnectionOptimizer.Views;

/// <summary>Shown instead of the dashboard on a PC without a valid license.</summary>
public partial class LockWindow : Window
{
    private readonly LicenseService _licensing;

    public LockWindow(LicenseService licensing, LicenseCheck check)
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        _licensing = licensing;
        HwidText.Text = check.Hwid ?? "UNAVAILABLE";
        CopyButton.IsEnabled = check.Hwid is not null;
        ShowMessage(check);
    }

    /// <summary>Raised once a valid license has been installed.</summary>
    public event EventHandler<LicenseCheck>? Unlocked;

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(HwidText.Text);
            CopyButton.Content = "✓";
        }
        catch (COMException)
        {
            // Clipboard busy: the ID is still visible to copy by hand.
        }
    }

    private void OnLoadLicense(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Load G.S.9 license",
            Filter = "G.S.9 license (*.key)|*.key|All files (*.*)|*.*",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        LicenseCheck check = _licensing.Import(dialog.FileName);
        if (check.IsValid)
        {
            Unlocked?.Invoke(this, check);
            return;
        }

        ShowMessage(check);
    }

    private void ShowMessage(LicenseCheck check)
    {
        // A missing license is the normal first run, not an error.
        bool isError = check.State == LicenseState.Invalid;
        MessagePanel.Visibility = isError ? Visibility.Visible : Visibility.Collapsed;
        MessageText.Text = check.Message;
        MessageText.Foreground = (System.Windows.Media.Brush)FindResource(isError ? "Brush.Error" : "Brush.TextSecondary");
    }
}
