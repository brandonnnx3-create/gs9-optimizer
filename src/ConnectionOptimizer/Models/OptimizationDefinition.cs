namespace ConnectionOptimizer.Models;

public enum OptimizationSection
{
    Network,
    Cleanup,
}

/// <summary>
/// Describes one optimization: how to present it and which script runs it.
/// The optimization logic itself lives only in the script.
/// </summary>
public sealed record OptimizationDefinition
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required OptimizationSection Section { get; init; }

    /// <summary>File name inside the Scripts folder.</summary>
    public required string ScriptFile { get; init; }

    /// <summary>Optional read-only script that reports whether the changes are active.</summary>
    public string? VerifierScriptFile { get; init; }

    public bool RequiresAdministrator { get; init; } = true;
    public bool RestartRecommended { get; init; }

    /// <summary>Side effects the user should know about before running the script.</summary>
    public IReadOnlyList<string> Warnings { get; init; } = [];

    /// <summary>Shown while the script runs, when it needs the user's attention.</summary>
    public string? RunningHint { get; init; }
}
