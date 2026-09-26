namespace ConnectionOptimizer.Services;

/// <summary>What happened when a script ran. Only the exit code is known: batch scripts do not report per-command results.</summary>
public sealed record ScriptRunResult
{
    public required string ScriptFile { get; init; }
    public required DateTime StartedAt { get; init; }
    public TimeSpan Duration { get; init; }
    public int? ExitCode { get; init; }

    /// <summary>Set when the script could not be started at all.</summary>
    public string? FailureReason { get; init; }

    public bool TimedOut { get; init; }
    public IReadOnlyList<string> Output { get; init; } = [];
    public string? LogFilePath { get; init; }

    public bool Succeeded => FailureReason is null && !TimedOut && ExitCode == 0;

    public string Describe() =>
        FailureReason ?? (TimedOut ? "timed out and was stopped" : $"exit code {ExitCode}");

    public static ScriptRunResult NotStarted(string scriptFile, string reason) => new()
    {
        ScriptFile = scriptFile,
        StartedAt = DateTime.Now,
        FailureReason = reason,
    };
}
