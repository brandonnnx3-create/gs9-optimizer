using System.Globalization;
using System.Text.RegularExpressions;

namespace ConnectionOptimizer.Services;

public enum VerificationState
{
    Active,
    Partial,
    Inactive,
    Failed,
}

public sealed record VerificationResult
{
    public required VerificationState State { get; init; }
    public int Applied { get; init; }
    public int Total { get; init; }
    public string? InterfaceGuid { get; init; }
    public required string Summary { get; init; }
    public required ScriptRunResult Run { get; init; }

    public DateTime CheckedAt => Run.StartedAt;
}

/// <summary>Runs an existing verifier script and reads its result. The checks themselves are the script's.</summary>
public sealed class ScriptVerifier(ScriptRunner runner)
{
    private static readonly TimeSpan VerifierTimeout = TimeSpan.FromMinutes(2);

    public async Task<VerificationResult> VerifyAsync(string verifierScriptFile, string logName)
    {
        ScriptRunResult run = await runner.RunAsync(verifierScriptFile, logName, output: null, VerifierTimeout)
            .ConfigureAwait(false);
        return VerifierOutputParser.Parse(run);
    }
}

/// <summary>
/// Reads the summary printed by Verificar_Network_Tweaks.bat:
/// "Resultado: 7 OK, 1 distintos, 2 faltantes (de 10)." and its exit code (0 = all values applied).
/// </summary>
public static partial class VerifierOutputParser
{
    public static VerificationResult Parse(ScriptRunResult run)
    {
        if (run.FailureReason is not null || run.TimedOut)
        {
            return Failed(run, $"The verifier {run.Describe()}.");
        }

        Match? summary = null;
        string? interfaceGuid = null;
        bool interfaceMissing = false;

        foreach (string line in run.Output)
        {
            Match match = SummaryLine().Match(line);
            if (match.Success)
            {
                summary = match;
            }

            Match guid = GuidLine().Match(line);
            if (guid.Success)
            {
                interfaceGuid = guid.Groups["guid"].Value;
            }

            if (line.Contains("No se detecto la interfaz", StringComparison.OrdinalIgnoreCase))
            {
                interfaceMissing = true;
            }
        }

        if (summary is null)
        {
            return Failed(run, $"Verifier output not recognized (exit code {run.ExitCode}).");
        }

        int applied = ReadInt(summary, "ok");
        int different = ReadInt(summary, "diff");
        int missing = ReadInt(summary, "missing");
        int total = ReadInt(summary, "total");

        VerificationState state =
            applied == total && run.ExitCode == 0 ? VerificationState.Active
            : applied == 0 ? VerificationState.Inactive
            : VerificationState.Partial;

        string text = $"{applied}/{total} values applied";
        if (different > 0)
        {
            text += $" · {different} different";
        }

        if (missing > 0)
        {
            text += $" · {missing} missing";
        }

        if (interfaceMissing)
        {
            text += " · active interface not detected";
        }

        return new VerificationResult
        {
            State = state,
            Applied = applied,
            Total = total,
            InterfaceGuid = interfaceGuid,
            Summary = text,
            Run = run,
        };
    }

    private static VerificationResult Failed(ScriptRunResult run, string summary) => new()
    {
        State = VerificationState.Failed,
        Summary = summary,
        Run = run,
    };

    private static int ReadInt(Match match, string group) =>
        int.Parse(match.Groups[group].Value, CultureInfo.InvariantCulture);

    [GeneratedRegex(@"Resultado:\s*(?<ok>\d+)\s*OK,\s*(?<diff>\d+)\s*distintos,\s*(?<missing>\d+)\s*faltantes\s*\(de\s*(?<total>\d+)\)", RegexOptions.IgnoreCase)]
    private static partial Regex SummaryLine();

    [GeneratedRegex(@"GUID:\s*(?<guid>\{[0-9A-Fa-f-]{36}\})")]
    private static partial Regex GuidLine();
}
