using System.IO;
using System.Windows;
using System.Windows.Threading;
using ConnectionOptimizer.Services;
using ConnectionOptimizer.ViewModels;
using ConnectionOptimizer.Views;

namespace ConnectionOptimizer;

/// <summary>Composition root: builds the services and the main window.</summary>
public partial class App : Application
{
    private static readonly string LogsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GS9", "ConnectionOptimizer", "Logs");

    private ActivityLogViewModel? _activity;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += OnDispatcherUnhandledException;

        var runner = new ScriptRunner(Path.Combine(AppContext.BaseDirectory, "Scripts"), LogsDirectory);
        _activity = new ActivityLogViewModel(LogsDirectory);
        AsyncRelayCommand.UnhandledException = ex => _activity.Error($"Unexpected error · {ex.Message}");

        var window = new MainWindow();
        var viewModel = new MainViewModel(
            runner,
            new ScriptVerifier(runner),
            new NetworkInfoService(),
            new UiService(window),
            _activity);

        window.DataContext = viewModel;
        MainWindow = window;
        window.Show();

        await viewModel.InitializeAsync(StartupRequest.Parse(e.Args));
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
