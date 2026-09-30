using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using ConnectionOptimizer.Services.Licensing;

namespace ConnectionOptimizer.Views;

/// <summary>Shown while access is checked, and instead of the dashboard when this PC is not authorized.</summary>
public partial class LockWindow : Window
{
    private readonly AccessController _access;

    public LockWindow(AccessController access)
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        _access = access;
        ShowChecking();
    }

    /// <summary>Raised once access is confirmed, with the confirmed result.</summary>
    public event EventHandler<AccessResult>? Granted;

    /// <summary>Runs the access check and either raises <see cref="Granted"/> or shows why it was denied.</summary>
    public async Task RunCheckAsync()
    {
        ShowChecking();
        AccessResult result = await _access.CheckAsync();

        if (result.Hwid is not null)
        {
            HwidText.Text = result.Hwid;
        }

        CopyButton.IsEnabled = result.Hwid is not null;

        if (result.IsAllowed)
        {
            Granted?.Invoke(this, result);
            return;
        }

        ShowDenied(result);
    }

    private void ShowChecking()
    {
        HeadlineText.Text = "CHECKING ACCESS…";
        MessageText.Text = "Verifying this PC with the G.S.9 server.";
        MessageGlyph.Text = "›";
        MessageGlyph.Foreground = Brush("Brush.TextSecondary");
        RetryButton.IsEnabled = false;
        RetryText.Text = "CHECKING…";
    }

    private void ShowDenied(AccessResult result)
    {
        (string headline, bool isError) = result.State switch
        {
            AccessState.NotAuthorized => ("THIS PC IS NOT AUTHORIZED", false),
            AccessState.Expired => ("ACCESS EXPIRED", true),
            AccessState.Unverified => ("COULD NOT VERIFY ACCESS", true),
            AccessState.NotConfigured => ("ACCESS NOT CONFIGURED", true),
            AccessState.NoHardwareId => ("HARDWARE ID UNAVAILABLE", true),
            _ => ("ACCESS DENIED", true),
        };

        HeadlineText.Text = headline;
        MessageText.Text = result.State == AccessState.NotAuthorized
            ? "Send this hardware ID to G.S.9 to be authorized, then re-check."
            : result.Message;
        MessageGlyph.Text = isError ? "✕" : "›";
        MessageGlyph.Foreground = Brush(isError ? "Brush.Error" : "Brush.TextSecondary");
        MessageText.Foreground = Brush(isError ? "Brush.Error" : "Brush.TextSecondary");
        RetryButton.IsEnabled = true;
        RetryText.Text = "RE-CHECK ACCESS";
    }

    private async void OnRetry(object sender, RoutedEventArgs e) => await RunCheckAsync();

    private void OnCopy(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(HwidText.Text);
            CopyButton.Content = "✓";
        }
        catch (COMException)
        {
            // Clipboard busy: the ID is still on screen to copy by hand.
        }
    }

    private Brush Brush(string key) => (Brush)FindResource(key);
}
