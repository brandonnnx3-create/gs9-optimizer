using System.Diagnostics;
using System.IO;
using System.Windows;
using ConnectionOptimizer.ViewModels;

namespace ConnectionOptimizer.Views;

public partial class LogWindow : Window
{
    private readonly LogView _log;

    public LogWindow(LogView log)
    {
        InitializeComponent();
        WindowTheming.UseDarkChrome(this);
        _log = log;
        DataContext = log;

        if (log.IsError)
        {
            ResultText.Foreground = (System.Windows.Media.Brush)FindResource("Brush.Error");
        }

        OpenFolderButton.IsEnabled = log.LogFile is not null && File.Exists(log.LogFile);
    }

    private void OnShowLogFile(object sender, RoutedEventArgs e)
    {
        if (_log.LogFile is { } file && File.Exists(file))
        {
            Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{file}\"") { UseShellExecute = true });
        }
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
