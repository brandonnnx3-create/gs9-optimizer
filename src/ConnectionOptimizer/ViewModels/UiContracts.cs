using ConnectionOptimizer.Services;

namespace ConnectionOptimizer.ViewModels;

/// <summary>Windows and shell actions the view models need, implemented by the view layer.</summary>
public interface IUiService
{
    bool Confirm(ConfirmRequest request);

    void Shutdown();
}

public sealed record ConfirmRequest
{
    public required string Label { get; init; }
    public required string Title { get; init; }
    public required string Message { get; init; }
    public IReadOnlyList<string> Items { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public string? NoteTitle { get; init; }
    public string? Note { get; init; }
    public required string ConfirmText { get; init; }
    public string CancelText { get; init; } = "CANCEL";

    public bool HasItems => Items.Count > 0;
    public bool HasWarnings => Warnings.Count > 0;
}

/// <summary>The error banner: readable first, technical detail second.</summary>
public sealed record AlertViewModel
{
    public required OptimizationViewModel Module { get; init; }
    public required string Title { get; init; }
    public required string Subject { get; init; }
    public required string Message { get; init; }
    public required string Detail { get; init; }

    public static AlertViewModel ForFailure(OptimizationViewModel module, ScriptRunResult result) => new()
    {
        Module = module,
        Title = "OPTIMIZATION FAILED",
        Subject = module.Name,
        Message = result switch
        {
            { FailureReason: not null } => "The script could not be started.",
            { TimedOut: true } => "The script did not finish in time and was stopped.",
            _ => "The script ended with an error code: at least one of its commands failed.",
        },
        Detail = result.FailureReason ?? (result.TimedOut ? "Timed out" : $"Exit code: {result.ExitCode}"),
    };
}

/// <summary>What to do right after start-up; used to continue an action after the UAC restart.</summary>
public sealed record StartupRequest(string? RunId, bool RunAll)
{
    public const string RunArgument = "--run";
    public const string RunAllArgument = "--run-all";

    public static StartupRequest None { get; } = new(null, false);

    public static StartupRequest Parse(IReadOnlyList<string> args)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (args[i].Equals(RunAllArgument, StringComparison.OrdinalIgnoreCase))
            {
                return new StartupRequest(null, true);
            }

            if (args[i].Equals(RunArgument, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Count)
            {
                return new StartupRequest(args[i + 1], false);
            }
        }

        return None;
    }
}
