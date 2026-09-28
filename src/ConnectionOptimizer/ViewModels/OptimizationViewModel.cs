using System.Windows.Input;
using ConnectionOptimizer.Models;
using ConnectionOptimizer.Services;

namespace ConnectionOptimizer.ViewModels;

public interface IOptimizationHost
{
    bool IsBusy { get; }

    Task ActivateAsync(OptimizationViewModel module);

    Task CheckStatusAsync(OptimizationViewModel module);

    void ShowScriptWindow(OptimizationViewModel module);
}

/// <summary>
/// One optimization tile. The status it shows is only what is known:
/// a script's exit code, or — when a verifier exists — what the verifier reported.
/// </summary>
public sealed class OptimizationViewModel : ObservableObject
{
    private enum RunState
    {
        Ready,
        Running,
        Applied,
        Error,
    }

    private RunState _runState = RunState.Ready;
    private ScriptRunResult? _lastRun;
    private VerificationResult? _verification;
    private bool _isChecking;
    private bool _showRunError;
    private TimeSpan _elapsed;
    private string? _hint;
    private ScriptWindow? _openWindow;

    public OptimizationViewModel(
        OptimizationDefinition definition,
        int position,
        bool isScriptAvailable,
        bool isVerifierAvailable,
        IOptimizationHost host)
    {
        Definition = definition;
        Number = position.ToString("00");
        IsScriptAvailable = isScriptAvailable;
        IsVerifierAvailable = isVerifierAvailable;

        ActivateCommand = new AsyncRelayCommand(
            () => host.ActivateAsync(this),
            () => IsScriptAvailable && !host.IsBusy);
        CheckStatusCommand = new AsyncRelayCommand(
            () => host.CheckStatusAsync(this),
            () => HasVerifier && IsVerifierAvailable && !host.IsBusy);
        ShowWindowCommand = new RelayCommand(() => host.ShowScriptWindow(this), () => OpenWindow is not null);
    }

    public OptimizationDefinition Definition { get; }
    public string Number { get; }
    public string Name => Definition.Name;
    public string Description => Definition.Description;
    public string Category => Definition.Category;
    public bool IsScriptAvailable { get; }
    public bool HasVerifier => Definition.VerifierScriptFile is not null;
    public bool IsVerifierAvailable { get; }

    public ICommand ActivateCommand { get; }
    public ICommand CheckStatusCommand { get; }
    public ICommand ShowWindowCommand { get; }

    /// <summary>A window opened by the running script (it may be waiting for the user).</summary>
    public ScriptWindow? OpenWindow
    {
        get => _openWindow;
        private set
        {
            if (SetProperty(ref _openWindow, value))
            {
                OnPropertyChanged(nameof(HasOpenWindow));
                RaiseStatusChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }

    public bool HasOpenWindow => _openWindow is not null;

    public string LastRunDescription => _lastRun?.Describe() ?? "not run";

    /// <summary>Our own note while the script runs (never the script's output).</summary>
    public string? Hint
    {
        get => _hint;
        private set => SetProperty(ref _hint, value);
    }

    public StatusKind StatusKind => ComputeStatus().Kind;
    public string StatusText => ComputeStatus().Text;
    public string StatusCaption => ComputeStatus().Caption;

    internal void MarkRunning()
    {
        _runState = RunState.Running;
        _showRunError = false;
        _elapsed = TimeSpan.Zero;
        Hint = Definition.RunningHint;
        RaiseStatusChanged();
    }

    internal void UpdateElapsed(TimeSpan elapsed)
    {
        _elapsed = elapsed;
        OnPropertyChanged(nameof(StatusCaption));
    }

    internal void SetOpenWindow(ScriptWindow? window)
    {
        if (_runState == RunState.Running || window is null)
        {
            OpenWindow = window;
        }
    }

    internal void MarkRunFinished(ScriptRunResult result)
    {
        OpenWindow = null;
        _lastRun = result;
        _runState = result.Succeeded ? RunState.Applied : RunState.Error;
        _showRunError = !result.Succeeded;
        Hint = null;
        RaiseStatusChanged();
    }

    internal void MarkChecking()
    {
        _isChecking = true;
        RaiseStatusChanged();
    }

    /// <param name="replacesRunError">True when the user asked for the check, so its result becomes the visible state.</param>
    internal void MarkVerified(VerificationResult result, bool replacesRunError)
    {
        _isChecking = false;
        _verification = result;
        if (replacesRunError)
        {
            _showRunError = false;
        }

        RaiseStatusChanged();
    }

    private (StatusKind Kind, string Text, string Caption) ComputeStatus()
    {
        if (!IsScriptAvailable)
        {
            return (StatusKind.Error, "MISSING", "Script file not found");
        }

        if (_runState == RunState.Running)
        {
            return _openWindow is { } window
                ? (StatusKind.Waiting, "WINDOW OPEN", $"\"{window.Title}\" may need your input · {Format.Elapsed(_elapsed)}")
                : (StatusKind.Running, "RUNNING…", $"elapsed {Format.Elapsed(_elapsed)}");
        }

        if (_isChecking)
        {
            return (StatusKind.Checking, "CHECKING…", "Running the verifier");
        }

        if (_showRunError && _lastRun is not null)
        {
            string check = _verification is { State: not VerificationState.Failed } v
                ? $" · check {v.Applied}/{v.Total}"
                : string.Empty;
            return (StatusKind.Error, "ERROR", RunSummary(_lastRun) + check);
        }

        if (HasVerifier)
        {
            return ComputeVerifiedStatus();
        }

        return _runState switch
        {
            RunState.Applied => (StatusKind.Applied, "APPLIED", $"{RunSummary(_lastRun!)} · unverified"),
            RunState.Error => (StatusKind.Error, "ERROR", RunSummary(_lastRun!)),
            _ => (StatusKind.Ready, "READY", "Not run in this session"),
        };
    }

    private (StatusKind Kind, string Text, string Caption) ComputeVerifiedStatus()
    {
        if (!IsVerifierAvailable)
        {
            return (StatusKind.Error, "NO VERIFIER", "Verifier file not found");
        }

        if (_verification is not { } v)
        {
            return (StatusKind.Ready, "NOT CHECKED", "Status not checked yet");
        }

        string checkedAt = $"checked {Format.Clock(v.CheckedAt)}";
        string lastRun = _lastRun is { Succeeded: true } run ? $" · last run exit 0 {Format.Clock(run.StartedAt)}" : string.Empty;

        return v.State switch
        {
            VerificationState.Active => (StatusKind.Active, "ACTIVE", $"{v.Applied}/{v.Total} values · {checkedAt}{lastRun}"),
            VerificationState.Partial => (StatusKind.Partial, "PARTIAL", $"{v.Summary} · {checkedAt}"),
            VerificationState.Inactive => (StatusKind.Inactive, "INACTIVE", $"{v.Summary} · {checkedAt}"),
            _ => (StatusKind.Error, "CHECK FAILED", v.Summary),
        };
    }

    private static string RunSummary(ScriptRunResult run) =>
        run.FailureReason is not null
            ? run.FailureReason
            : $"{(run.TimedOut ? "timed out" : $"exit {run.ExitCode}")} · {Format.Duration(run.Duration)} · {Format.Clock(run.StartedAt)}";

    private void RaiseStatusChanged()
    {
        OnPropertyChanged(nameof(StatusKind));
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(StatusCaption));
    }
}
