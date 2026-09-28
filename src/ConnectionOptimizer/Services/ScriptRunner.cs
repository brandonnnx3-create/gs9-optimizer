using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConnectionOptimizer.Services;

/// <summary>
/// Runs the scripts from the Scripts folder through cmd.exe, one at a time.
/// The scripts are executed as they are: nothing is rewritten or injected.
/// Nothing is written to disk: script output is not read at all, except for the verifier,
/// whose output is parsed in memory and discarded.
/// </summary>
public sealed class ScriptRunner
{
    /// <summary>How long to wait for the output pipes after cmd.exe exits (a child process may keep them open).</summary>
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(3);

    private static readonly Encoding ConsoleEncoding = CreateConsoleEncoding();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile int _runningProcessId;

    public ScriptRunner(string scriptsDirectory)
    {
        ScriptsDirectory = Path.GetFullPath(scriptsDirectory);
    }

    public string ScriptsDirectory { get; }

    /// <summary>Process id of the cmd.exe running the current script, or null when idle.</summary>
    public int? RunningProcessId => _runningProcessId == 0 ? null : _runningProcessId;

    public bool Exists(string scriptFile) => File.Exists(ResolvePath(scriptFile));

    /// <summary>Resolves a file name inside the Scripts folder; anything outside it is rejected.</summary>
    public string ResolvePath(string scriptFile)
    {
        string fullPath = Path.GetFullPath(Path.Combine(ScriptsDirectory, scriptFile));
        string root = Path.TrimEndingDirectorySeparator(ScriptsDirectory) + Path.DirectorySeparatorChar;
        if (!fullPath.StartsWith(root, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"'{scriptFile}' is outside the Scripts folder.", nameof(scriptFile));
        }

        return fullPath;
    }

    /// <summary>
    /// Runs a script and waits for it to finish. Only one script runs at a time.
    /// </summary>
    /// <param name="captureOutput">Read the script's output into memory (only the verifier needs it).</param>
    /// <param name="timeout">Null waits indefinitely (SFC/DISM can take a long time).</param>
    public async Task<ScriptRunResult> RunAsync(string scriptFile, bool captureOutput = false, TimeSpan? timeout = null)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false))
        {
            return ScriptRunResult.NotStarted(scriptFile, "Another script is already running.");
        }

        try
        {
            // Off the UI thread: starting cmd.exe is synchronous.
            return await Task.Run(() => RunCoreAsync(scriptFile, captureOutput, timeout)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ScriptRunResult> RunCoreAsync(string scriptFile, bool captureOutput, TimeSpan? timeout)
    {
        string scriptPath = ResolvePath(scriptFile);
        if (!File.Exists(scriptPath))
        {
            return ScriptRunResult.NotStarted(scriptFile, "Script file not found.");
        }

        DateTime startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();
        var lines = new List<string>();
        var sync = new object();
        bool collecting = true;

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            // /d skips AutoRun commands. cmd strips the outer quotes and keeps the path quoted.
            Arguments = $"/d /c \"\"{scriptPath}\"\"",
            WorkingDirectory = ScriptsDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
        };

        if (captureOutput)
        {
            psi.StandardOutputEncoding = ConsoleEncoding;
            psi.StandardErrorEncoding = ConsoleEncoding;
        }

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();
        if (captureOutput)
        {
            process.OutputDataReceived += (_, e) => OnLine(e.Data);
            process.ErrorDataReceived += (_, e) => OnLine(e.Data);
        }

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return ScriptRunResult.NotStarted(scriptFile, $"Could not start cmd.exe: {ex.Message}");
        }

        _runningProcessId = process.Id;

        // Every script ends with PAUSE. Closing stdin gives it end-of-input, so it returns instead of waiting for a key.
        process.StandardInput.Close();
        if (captureOutput)
        {
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();
        }

        bool timedOut = false;
        using (var timeoutCts = new CancellationTokenSource(timeout ?? Timeout.InfiniteTimeSpan))
        {
            try
            {
                await exited.Task.WaitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                timedOut = true;
                TryKill(process);
            }
        }

        _runningProcessId = 0;
        if (captureOutput)
        {
            await DrainOutputAsync(process).ConfigureAwait(false);
        }

        stopwatch.Stop();
        string[] output;
        lock (sync)
        {
            collecting = false;
            output = lines.ToArray();
        }

        return new ScriptRunResult
        {
            ScriptFile = scriptFile,
            StartedAt = startedAt,
            Duration = stopwatch.Elapsed,
            ExitCode = timedOut ? null : process.ExitCode,
            TimedOut = timedOut,
            Output = output,
        };

        void OnLine(string? raw)
        {
            if (raw is null)
            {
                return;
            }

            // SFC writes UTF-16 when its output is redirected; dropping the NULs keeps it readable.
            string line = raw.Replace("\0", string.Empty);
            if (line.Length == 0 && raw.Length > 0)
            {
                return;
            }

            lock (sync)
            {
                if (collecting)
                {
                    lines.Add(line);
                }
            }
        }
    }

    private static async Task DrainOutputAsync(Process process)
    {
        using var drainCts = new CancellationTokenSource(OutputDrainTimeout);
        try
        {
            await process.WaitForExitAsync(drainCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // A process started by the script still holds the output pipe. The script itself already finished.
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Already exited.
        }
    }

    private static Encoding CreateConsoleEncoding()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try
        {
            return Encoding.GetEncoding((int)GetOEMCP());
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException)
        {
            return Encoding.Default;
        }
    }

    [DllImport("kernel32.dll")]
    private static extern uint GetOEMCP();
}
