using System.Net.NetworkInformation;
using System.Reflection;
using System.Windows.Input;
using System.Windows.Threading;
using ConnectionOptimizer.Models;
using ConnectionOptimizer.Services;

namespace ConnectionOptimizer.ViewModels;

public enum SystemState
{
    Ready,
    Optimizing,
    Completed,
    Error,
}

/// <summary>
/// Dashboard state and the flows behind it: run one script, run all in sequence, check status, read the connection.
/// Only one script (or verifier) runs at a time; everything else waits on <see cref="IsBusy"/>.
/// </summary>
public sealed class MainViewModel : ObservableObject, IOptimizationHost
{
    private readonly ScriptRunner _runner;
    private readonly ScriptVerifier _verifier;
    private readonly NetworkInfoService _network;
    private readonly IUiService _ui;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly DispatcherTimer _clock;
    private readonly DispatcherTimer _networkDebounce;
    private readonly HashSet<OptimizationViewModel> _verifiedSinceLastRun = [];
    private readonly HashSet<IntPtr> _announcedWindows = [];
    private readonly string? _licensedTo;

    private bool _isBusy;
    private SystemState _systemState = SystemState.Ready;
    private string _systemDetail = "No scripts have run in this session.";
    private AlertViewModel? _alert;
    private bool _isBatchVisible;
    private bool _isBatchRunning;
    private string _batchHeadline = string.Empty;
    private string _batchCounter = string.Empty;
    private string _batchCurrent = string.Empty;
    private OptimizationViewModel? _runningModule;
    private DateTime _runningSince;
    private bool _refreshingConnection;
    private bool _refreshAgain;
    private string? _lastConnectionKey;
    private bool _watchingWindows;

    public MainViewModel(
        ScriptRunner runner,
        ScriptVerifier verifier,
        NetworkInfoService network,
        IUiService ui,
        ActivityLogViewModel activity,
        string? licensedTo = null)
    {
        _runner = runner;
        _licensedTo = licensedTo;
        _verifier = verifier;
        _network = network;
        _ui = ui;
        Activity = activity;

        Modules = OptimizationCatalog.All
            .Select((definition, index) => new OptimizationViewModel(
                definition,
                index + 1,
                runner.Exists(definition.ScriptFile),
                definition.VerifierScriptFile is { } verifierFile && runner.Exists(verifierFile),
                this))
            .ToList();
        NetworkModules = Modules.Where(m => m.Definition.Section == OptimizationSection.Network).ToList();
        CleanupModules = Modules.Where(m => m.Definition.Section == OptimizationSection.Cleanup).ToList();
        BatchSteps = Modules.Select(m => new BatchStepViewModel(m.Number, m.Name)).ToList();

        ActivateAllCommand = new AsyncRelayCommand(ActivateAllAsync, () => !IsBusy && Modules.All(m => m.IsScriptAvailable));
        RefreshConnectionCommand = new AsyncRelayCommand(RefreshConnectionAsync);
        ElevateCommand = new RelayCommand(() => RestartElevated(string.Empty), () => !IsElevated && !IsBusy);
        OpenLogsFolderCommand = new RelayCommand(() => _ui.OpenFolder(_runner.LogsDirectory));
        ViewAlertLogCommand = new RelayCommand(() => ViewLog(Alert!.Module), () => Alert?.Module.HasLog == true);
        DismissAlertCommand = new RelayCommand(() => Alert = null);

        _clock = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _clock.Tick += (_, _) => OnClockTick();
        _networkDebounce = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };
        _networkDebounce.Tick += async (_, _) =>
        {
            _networkDebounce.Stop();
            await RefreshConnectionAsync();
        };
    }

    public IReadOnlyList<OptimizationViewModel> Modules { get; }
    public IReadOnlyList<OptimizationViewModel> NetworkModules { get; }
    public IReadOnlyList<OptimizationViewModel> CleanupModules { get; }
    public IReadOnlyList<BatchStepViewModel> BatchSteps { get; }
    public ConnectionViewModel Connection { get; } = new();
    public ActivityLogViewModel Activity { get; }

    public ICommand ActivateAllCommand { get; }
    public ICommand RefreshConnectionCommand { get; }
    public ICommand ElevateCommand { get; }
    public ICommand OpenLogsFolderCommand { get; }
    public ICommand ViewAlertLogCommand { get; }
    public ICommand DismissAlertCommand { get; }

    public bool IsElevated => Elevation.IsElevated;
    public string PrivilegesText => IsElevated ? "ADMINISTRATOR" : "STANDARD USER";
    public string PrivilegesDetail => IsElevated ? "Scripts can run now." : "UAC is requested only when a script runs.";

    public string FooterText =>
        $"G.S.9 CONNECTION OPTIMIZER  v{Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)}" +
        (_licensedTo is null ? string.Empty : $"      LICENSED TO  {_licensedTo}") +
        $"      SCRIPTS  {_runner.ScriptsDirectory}";

    public string NetworkCount => $"{NetworkModules.Count:00} TOOLS";
    public string CleanupCount => $"{CleanupModules.Count:00} TOOLS";
    public string ActivateAllHint => $"{Modules.Count} SCRIPTS  ·  ONE BY ONE  →";

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public SystemState SystemState
    {
        get => _systemState;
        private set
        {
            if (SetProperty(ref _systemState, value))
            {
                OnPropertyChanged(nameof(SystemStatusKind));
                OnPropertyChanged(nameof(SystemStatusText));
            }
        }
    }

    public StatusKind SystemStatusKind => SystemState switch
    {
        SystemState.Optimizing => StatusKind.Optimizing,
        SystemState.Completed => StatusKind.Completed,
        SystemState.Error => StatusKind.Error,
        _ => StatusKind.Ready,
    };

    public string SystemStatusText => SystemState.ToString().ToUpperInvariant();

    public string SystemDetail
    {
        get => _systemDetail;
        private set => SetProperty(ref _systemDetail, value);
    }

    public AlertViewModel? Alert
    {
        get => _alert;
        private set
        {
            if (SetProperty(ref _alert, value))
            {
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool IsBatchVisible
    {
        get => _isBatchVisible;
        private set => SetProperty(ref _isBatchVisible, value);
    }

    public bool IsBatchRunning
    {
        get => _isBatchRunning;
        private set => SetProperty(ref _isBatchRunning, value);
    }

    public string BatchHeadline
    {
        get => _batchHeadline;
        private set => SetProperty(ref _batchHeadline, value);
    }

    public string BatchCounter
    {
        get => _batchCounter;
        private set => SetProperty(ref _batchCounter, value);
    }

    public string BatchCurrent
    {
        get => _batchCurrent;
        private set => SetProperty(ref _batchCurrent, value);
    }

    public async Task InitializeAsync(StartupRequest request)
    {
        Activity.Info($"Connection Optimizer started · {(IsElevated ? "administrator" : "standard user")}");
        foreach (OptimizationViewModel module in Modules.Where(m => !m.IsScriptAvailable))
        {
            Activity.Error($"{module.Name}: script not found · {module.Definition.ScriptFile}");
        }

        foreach (OptimizationViewModel module in Modules.Where(m => m.HasVerifier && !m.IsVerifierAvailable))
        {
            Activity.Error($"{module.Name}: verifier not found · {module.Definition.VerifierScriptFile}");
        }

        NetworkChange.NetworkAddressChanged += (_, _) => ScheduleConnectionRefresh();
        NetworkChange.NetworkAvailabilityChanged += (_, _) => ScheduleConnectionRefresh();

        await RefreshConnectionAsync();

        IsBusy = true;
        try
        {
            await VerifyAllAsync(replacesRunError: true);
        }
        finally
        {
            IsBusy = false;
        }

        await RunStartupRequestAsync(request);
    }

    /// <summary>Asked by the window before closing.</summary>
    public bool ConfirmClose() =>
        !IsBusy || _ui.Confirm(new ConfirmRequest
        {
            Label = "WARNING",
            Title = "A SCRIPT IS STILL RUNNING",
            Message = "Closing Connection Optimizer does not stop it: the script keeps running in the background and its result will not be shown.",
            ConfirmText = "CLOSE ANYWAY",
            CancelText = "KEEP OPEN",
        });

    public async Task ActivateAsync(OptimizationViewModel module)
    {
        if (IsBusy || !module.IsScriptAvailable)
        {
            return;
        }

        bool needsElevation = module.Definition.RequiresAdministrator && !IsElevated;
        ConfirmRequest? confirmation = BuildConfirmation([module], all: false, needsElevation);
        if (confirmation is not null && !_ui.Confirm(confirmation))
        {
            return;
        }

        if (needsElevation)
        {
            RestartElevated($"{StartupRequest.RunArgument} {module.Definition.Id}");
            return;
        }

        await RunSingleAsync(module);
    }

    public async Task CheckStatusAsync(OptimizationViewModel module)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await VerifyAsync(module, replacesRunError: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void ShowScriptWindow(OptimizationViewModel module)
    {
        if (module.OpenWindow is { } window)
        {
            ScriptWindows.BringToFront(window.Handle);
        }
    }

    public void ViewLog(OptimizationViewModel module)
    {
        if (module.LastLog is { } run)
        {
            _ui.ShowLog(LogView.From(module.Name, module.LastLogKind, run, module.LastLogIsError));
        }
    }

    private async Task ActivateAllAsync()
    {
        if (IsBusy)
        {
            return;
        }

        bool needsElevation = !IsElevated && Modules.Any(m => m.Definition.RequiresAdministrator);
        if (!_ui.Confirm(BuildConfirmation(Modules, all: true, needsElevation)!))
        {
            return;
        }

        if (needsElevation)
        {
            RestartElevated(StartupRequest.RunAllArgument);
            return;
        }

        await RunAllAsync();
    }

    private async Task RunStartupRequestAsync(StartupRequest request)
    {
        if (request.RunId is null && !request.RunAll)
        {
            return;
        }

        if (!IsElevated)
        {
            Activity.Warning("Start-up action ignored: the app is not running as administrator");
            return;
        }

        if (request.RunAll)
        {
            Activity.Info("Continuing ACTIVATE ALL after the administrator restart");
            await RunAllAsync();
            return;
        }

        OptimizationViewModel? module = Modules.FirstOrDefault(m => m.Definition.Id == request.RunId);
        if (module is null || !module.IsScriptAvailable)
        {
            Activity.Warning($"Start-up action ignored: unknown optimization '{request.RunId}'");
            return;
        }

        Activity.Info($"Continuing {module.Name} after the administrator restart");
        await RunSingleAsync(module);
    }

    private async Task RunSingleAsync(OptimizationViewModel module)
    {
        BeginOperation();
        bool succeeded = false;
        try
        {
            succeeded = await RunModuleAsync(module);
            await AfterScriptsAsync();
        }
        finally
        {
            EndOperation(
                succeeded,
                succeeded
                    ? $"{module.Name} finished with exit code 0.{RestartNote([module])}"
                    : $"{module.Name} failed. The log shows the script output.");
        }
    }

    private async Task RunAllAsync()
    {
        if (!Modules.All(m => m.IsScriptAvailable))
        {
            Activity.Error("ACTIVATE ALL not started: one or more scripts are missing");
            return;
        }

        BeginOperation();
        foreach (BatchStepViewModel step in BatchSteps)
        {
            step.State = StepState.Pending;
        }

        IsBatchVisible = true;
        IsBatchRunning = true;
        BatchHeadline = "APPLYING OPTIMIZATIONS";
        Activity.Info($"ACTIVATE ALL started · {Modules.Count} scripts");

        int completed = 0;
        int failedAt = -1;
        try
        {
            for (int i = 0; i < Modules.Count; i++)
            {
                OptimizationViewModel module = Modules[i];
                BatchCounter = $"{i + 1} / {Modules.Count}";
                BatchCurrent = $"{module.Name}  ·  RUNNING…";
                BatchSteps[i].State = StepState.Running;

                bool succeeded = await RunModuleAsync(module);
                BatchSteps[i].State = succeeded ? StepState.Done : StepState.Failed;
                if (!succeeded)
                {
                    failedAt = i;
                    break;
                }

                completed++;
            }

            await AfterScriptsAsync();
        }
        finally
        {
            foreach (BatchStepViewModel step in BatchSteps.Where(s => s.State is StepState.Pending or StepState.Running))
            {
                step.State = StepState.NotRun;
            }

            IsBatchRunning = false;
            bool allSucceeded = completed == Modules.Count;
            int total = Modules.Count;
            string detail;
            if (allSucceeded)
            {
                BatchHeadline = "SEQUENCE COMPLETED";
                BatchCounter = $"{total} / {total}";
                detail = $"{total}/{total} scripts finished with exit code 0.{RestartNote(Modules)}";
                Activity.Success($"ACTIVATE ALL completed · {total}/{total} scripts · exit 0");
            }
            else
            {
                int stoppedAt = failedAt >= 0 ? failedAt : completed;
                string failedName = Modules[Math.Min(stoppedAt, total - 1)].Name;
                BatchHeadline = $"SEQUENCE STOPPED AT {stoppedAt + 1} / {total}";
                BatchCounter = $"{completed} / {total}";
                detail = $"Stopped at {stoppedAt + 1}/{total}: {failedName} failed. The remaining scripts were not run.";
                Activity.Error($"ACTIVATE ALL stopped at {stoppedAt + 1}/{total} · {failedName} failed");
            }

            BatchCurrent = string.Empty;
            EndOperation(allSucceeded, detail);
        }
    }

    /// <summary>Runs one module's script and records the result. Never throws.</summary>
    private async Task<bool> RunModuleAsync(OptimizationViewModel module)
    {
        _runningModule = module;
        _runningSince = DateTime.Now;
        _verifiedSinceLastRun.Clear();
        _announcedWindows.Clear();
        module.MarkRunning();
        SystemDetail = $"Running {module.Name}…";
        Activity.Info($"{module.Name} started · {module.Definition.ScriptFile}");

        ScriptRunResult result;
        try
        {
            var output = new Progress<string>(module.ReportOutput);
            result = await _runner.RunAsync(module.Definition.ScriptFile, module.Definition.Id, output);
        }
        catch (Exception ex)
        {
            result = ScriptRunResult.NotStarted(module.Definition.ScriptFile, ex.Message);
        }
        finally
        {
            _runningModule = null;
        }

        module.MarkRunFinished(result);
        if (result.Succeeded)
        {
            Activity.Success($"{module.Name} completed · exit 0 · {Format.Duration(result.Duration)}");
        }
        else
        {
            Activity.Error($"{module.Name} failed · {result.Describe()}");
            Alert = AlertViewModel.ForFailure(module, result);
        }

        // A module with its own verifier is checked right away, so its tile shows the real state during a sequence.
        if (module.HasVerifier && module.IsVerifierAvailable)
        {
            await VerifyAsync(module, replacesRunError: false);
        }

        return result.Succeeded;
    }

    /// <summary>Scripts can change registry values and reset the adapter: re-read both (skips checks already up to date).</summary>
    private async Task AfterScriptsAsync()
    {
        await VerifyAllAsync(replacesRunError: false);
        await RefreshConnectionAsync();
    }

    private async Task VerifyAllAsync(bool replacesRunError)
    {
        foreach (OptimizationViewModel module in Modules.Where(m =>
                     m.HasVerifier && m.IsVerifierAvailable && !_verifiedSinceLastRun.Contains(m)))
        {
            await VerifyAsync(module, replacesRunError);
        }
    }

    private async Task VerifyAsync(OptimizationViewModel module, bool replacesRunError)
    {
        module.MarkChecking();
        VerificationResult result;
        try
        {
            result = await _verifier.VerifyAsync(module.Definition.VerifierScriptFile!, $"{module.Definition.Id}-check");
        }
        catch (Exception ex)
        {
            ScriptRunResult run = ScriptRunResult.NotStarted(module.Definition.VerifierScriptFile!, ex.Message);
            result = VerifierOutputParser.Parse(run);
        }

        module.MarkVerified(result, replacesRunError);
        _verifiedSinceLastRun.Add(module);
        string message = $"{module.Name} status checked · ";
        switch (result.State)
        {
            case VerificationState.Active:
                Activity.Success(message + $"ACTIVE ({result.Applied}/{result.Total})");
                break;
            case VerificationState.Partial:
                Activity.Warning(message + $"PARTIAL ({result.Summary})");
                break;
            case VerificationState.Inactive:
                Activity.Info(message + $"INACTIVE ({result.Applied}/{result.Total})");
                break;
            default:
                Activity.Error(message + $"FAILED ({result.Summary})");
                break;
        }
    }

    private async Task RefreshConnectionAsync()
    {
        if (_refreshingConnection)
        {
            _refreshAgain = true;
            return;
        }

        _refreshingConnection = true;
        Connection.IsLoading = true;
        try
        {
            do
            {
                _refreshAgain = false;
                ConnectionInfo info = await _network.GetActiveConnectionAsync();
                Connection.Update(info);
                LogConnectionChange(info);
            }
            while (_refreshAgain);
        }
        catch (Exception ex)
        {
            Activity.Error($"Could not read the network configuration · {ex.Message}");
        }
        finally
        {
            _refreshingConnection = false;
            Connection.IsLoading = false;
        }
    }

    private void LogConnectionChange(ConnectionInfo info)
    {
        string key = info.Found ? $"{info.InterfaceGuid}|{info.Status}|{info.IPv4}" : "none";
        if (key == _lastConnectionKey)
        {
            return;
        }

        bool first = _lastConnectionKey is null;
        _lastConnectionKey = key;
        if (!info.Found)
        {
            Activity.Warning("No active network connection detected");
        }
        else if (first)
        {
            Activity.Info($"Connection detected · {info.Medium} · {info.Description} · {Format.SpeedText(info.SpeedBitsPerSecond)}");
        }
        else
        {
            Activity.Info($"Connection changed · {info.Medium} · {info.Status} · {info.IPv4 ?? "no IPv4"}");
        }
    }

    /// <summary>Network events arrive on background threads and in bursts; refresh once they settle.</summary>
    private void ScheduleConnectionRefresh() =>
        _dispatcher.BeginInvoke(() =>
        {
            _networkDebounce.Stop();
            _networkDebounce.Start();
        });

    private void RestartElevated(string arguments)
    {
        try
        {
            if (Elevation.TryRestartElevated(arguments))
            {
                Activity.Info("Restarting as administrator");
                _ui.Shutdown();
            }
            else
            {
                Activity.Warning("Administrator permission was not granted · nothing was run");
            }
        }
        catch (Exception ex)
        {
            Activity.Error($"Could not restart as administrator · {ex.Message}");
        }
    }

    private void BeginOperation()
    {
        IsBusy = true;
        Alert = null;
        SystemState = SystemState.Optimizing;
        _clock.Start();
    }

    private void EndOperation(bool succeeded, string detail)
    {
        _clock.Stop();
        SystemState = succeeded ? SystemState.Completed : SystemState.Error;
        SystemDetail = detail;
        IsBusy = false;
    }

    private void OnClockTick()
    {
        if (_runningModule is not { } module)
        {
            return;
        }

        TimeSpan elapsed = DateTime.Now - _runningSince;
        module.UpdateElapsed(elapsed);
        if (IsBatchRunning)
        {
            BatchCurrent = module.OpenWindow is { } window
                ? $"{module.Name}  ·  WINDOW OPEN  ·  {window.Title}"
                : $"{module.Name}  ·  RUNNING  {Format.Elapsed(elapsed)}";
        }

        _ = WatchScriptWindowsAsync(module);
    }

    /// <summary>
    /// Some scripts open windows that wait for the user (Disk Cleanup settings). Detect them,
    /// bring them to the front once and say so, instead of showing a script that looks frozen.
    /// </summary>
    private async Task WatchScriptWindowsAsync(OptimizationViewModel module)
    {
        if (_watchingWindows || _runner.RunningProcessId is not int processId)
        {
            return;
        }

        _watchingWindows = true;
        try
        {
            IReadOnlyList<ScriptWindow> windows = await Task.Run(() => ScriptWindows.Find(processId));
            if (_runningModule != module)
            {
                return; // The script finished while looking.
            }

            ScriptWindow? window = windows.FirstOrDefault();
            module.SetOpenWindow(window);
            SystemDetail = window is null
                ? $"Running {module.Name}…"
                : $"{module.Name} is waiting on a window: \"{window.Title}\". It may need your input.";

            if (window is not null && _announcedWindows.Add(window.Handle))
            {
                Activity.Warning($"{module.Name} opened a window · \"{window.Title}\" ({window.ProcessName}) · it may need your input");
                ScriptWindows.BringToFront(window.Handle);
            }
        }
        catch (Exception)
        {
            // Best effort: failing to inspect windows must never affect the running script.
        }
        finally
        {
            _watchingWindows = false;
        }
    }

    private static string RestartNote(IEnumerable<OptimizationViewModel> modules) =>
        modules.Any(m => m.Definition.RestartRecommended) ? " Restart Windows to apply the changes." : string.Empty;

    private ConfirmRequest? BuildConfirmation(IReadOnlyList<OptimizationViewModel> modules, bool all, bool needsElevation)
    {
        List<string> warnings = modules.SelectMany(m => m.Definition.Warnings).Distinct().ToList();
        if (all && modules.Any(m => m.Definition.RestartRecommended))
        {
            warnings.Add("Restart Windows afterwards to apply the changes.");
        }

        if (!all && warnings.Count == 0 && !needsElevation)
        {
            return null;
        }

        OptimizationViewModel first = modules[0];
        return new ConfirmRequest
        {
            Label = all ? "SEQUENCE" : first.Category,
            Title = all ? "ACTIVATE ALL" : first.Name,
            Message = all
                ? $"Runs {modules.Count} scripts one by one, in this order. Stops at the first error."
                : $"Runs {first.Definition.ScriptFile}.",
            Items = all ? modules.Select(m => $"{m.Number}   {m.Name}   ·   {m.Definition.ScriptFile}").ToList() : [],
            Warnings = warnings,
            NoteTitle = needsElevation ? "ADMINISTRATOR REQUIRED" : null,
            Note = needsElevation
                ? $"Connection Optimizer will restart as administrator (UAC prompt) and then run {(all ? "the sequence" : "this script")} automatically."
                : null,
            ConfirmText = needsElevation ? "RESTART AS ADMIN" : all ? "ACTIVATE ALL" : "ACTIVATE",
        };
    }
}
