using System.IO;
using System.Windows;
using System.Windows.Threading;
using ConnectionOptimizer.Services;
using ConnectionOptimizer.Services.Licensing;
using ConnectionOptimizer.Services.Scripts;
using ConnectionOptimizer.ViewModels;
using ConnectionOptimizer.Views;

namespace ConnectionOptimizer;

/// <summary>Composition root: checks online access, then builds the services and the main window.</summary>
public partial class App : Application
{
    private static readonly string DataDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GS9", "ConnectionOptimizer");

    private ActivityLogViewModel? _activity;
    private string[] _args = [];
    private bool _dashboardStarted;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        DeleteOldData();
        _args = e.Args;

        // The dashboard is not created until the online check confirms this PC is authorized.
        var lockWindow = new LockWindow(new AccessController(DataDirectory));
        lockWindow.Granted += OnAccessGranted;
        MainWindow = lockWindow;
        lockWindow.Show();
        _ = lockWindow.RunCheckAsync();
    }

    private async void OnAccessGranted(object? sender, AccessResult access)
    {
        if (_dashboardStarted)
        {
            return;
        }

        _dashboardStarted = true;
        var lockWindow = sender as LockWindow;
        await StartDashboardAsync(access);
        lockWindow?.Close();
    }

    private async Task StartDashboardAsync(AccessResult access)
    {
        var runner = new ScriptRunner(ScriptStore.Load());
        _activity = new ActivityLogViewModel();
        AsyncRelayCommand.UnhandledException = ex => _activity.Error($"Unexpected error · {ex.Message}");

        var window = new MainWindow();
        var viewModel = new MainViewModel(
            runner,
            new ScriptVerifier(runner),
            new NetworkInfoService(),
            new UiService(window),
            _activity,
            access.Name);

        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();

        await viewModel.InitializeAsync(StartupRequest.Parse(_args));
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _activity?.Error($"Unexpected error · {e.Exception.Message}");
        MessageBox.Show(
            $"Connection Optimizer hit an unexpected error:\n\n{e.Exception.Message}",
            "G.S.9 — Connection Optimizer",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
        e.Handled = true;
    }

    /// <summary>Remove data written by earlier versions (script-output logs, per-PC license files).</summary>
    private static void DeleteOldData()
    {
        foreach (string path in new[] { Path.Combine(DataDirectory, "Logs"), Path.Combine(DataDirectory, "license.key") })
        {
            try
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
                else if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best effort; retried on the next start.
            }
        }
    }
}
