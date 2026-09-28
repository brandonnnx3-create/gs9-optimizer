using System.IO;
using System.Windows;
using System.Windows.Threading;
using ConnectionOptimizer.Services;
using ConnectionOptimizer.Services.Licensing;
using ConnectionOptimizer.ViewModels;
using ConnectionOptimizer.Views;

namespace ConnectionOptimizer;

/// <summary>Composition root: checks the license, then builds the services and the main window.</summary>
public partial class App : Application
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GS9", "ConnectionOptimizer");

    private static readonly string LogsDirectory = Path.Combine(DataDirectory, "Logs");

    private ActivityLogViewModel? _activity;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var licensing = new LicenseService(DataDirectory);
        LicenseCheck check = licensing.Check();
        if (check.IsValid)
        {
            await StartDashboardAsync(check, e.Args);
            return;
        }

        // Nothing else is created until this PC has a valid license.
        var lockWindow = new LockWindow(licensing, check);
        lockWindow.Unlocked += async (_, unlocked) =>
        {
            Task start = StartDashboardAsync(unlocked, e.Args); // Becomes the main window before the lock closes.
            lockWindow.Close();
            await start;
        };
        MainWindow = lockWindow;
        lockWindow.Show();
    }

    private async Task StartDashboardAsync(LicenseCheck license, string[] args)
    {
        var runner = new ScriptRunner(Path.Combine(AppContext.BaseDirectory, "Scripts"), LogsDirectory);
        _activity = new ActivityLogViewModel(LogsDirectory);
        AsyncRelayCommand.UnhandledException = ex => _activity.Error($"Unexpected error · {ex.Message}");

        var window = new MainWindow();
        var viewModel = new MainViewModel(
            runner,
            new ScriptVerifier(runner),
            new NetworkInfoService(),
            new UiService(window),
            _activity,
            license.LicensedTo);

        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();

        await viewModel.InitializeAsync(StartupRequest.Parse(args));
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        string? crashFile = WriteCrashLog(e.Exception);
        _activity?.Error($"Unexpected error · {e.Exception.Message}");
        MessageBox.Show(
            $"Connection Optimizer hit an unexpected error:\n\n{e.Exception.Message}" +
            (crashFile is null ? string.Empty : $"\n\nDetails saved to:\n{crashFile}"),
            "G.S.9 — Connection Optimizer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    private static string? WriteCrashLog(Exception exception)
    {
        try
        {
            Directory.CreateDirectory(LogsDirectory);
            string file = Path.Combine(LogsDirectory, $"crash-{DateTime.Now:yyyyMMdd-HHmmss}.log");
            File.WriteAllText(file, exception.ToString());
            return file;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
