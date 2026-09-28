using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace ConnectionOptimizer.Services;

/// <summary>
/// Runs the scripts from the Scripts folder through cmd.exe, one at a time, capturing their output to a log file.
/// The scripts are executed as they are: nothing is rewritten or injected.
/// </summary>
public sealed class ScriptRunner
{
    /// <summary>How long to wait for the output pipes after cmd.exe exits (a child process may keep them open).</summary>
    private static readonly TimeSpan OutputDrainTimeout = TimeSpan.FromSeconds(3);

    private static readonly Encoding ConsoleEncoding = CreateConsoleEncoding();

    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile int _runningProcessId;

    public ScriptRunner(string scriptsDirectory, string logsDirectory)
    {
        ScriptsDirectory = Path.GetFullPath(scriptsDirectory);
        LogsDirectory = Path.GetFullPath(logsDirectory);
    }

    public string ScriptsDirectory { get; }
    public string LogsDirectory { get; }

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
    /// <param name="timeout">Null waits indefinitely (SFC/DISM can take a long time).</param>
    public async Task<ScriptRunResult> RunAsync(
        string scriptFile, string logName, IProgress<string>? output = null, TimeSpan? timeout = null)
    {
        if (!await _gate.WaitAsync(0).ConfigureAwait(false))
        {
            return ScriptRunResult.NotStarted(scriptFile, "Another script is already running.");
        }

        try
        {
            // Off the UI thread: starting cmd.exe and opening the log file are synchronous.
            return await Task.Run(() => RunCoreAsync(scriptFile, logName, output, timeout)).ConfigureAwait(false);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<ScriptRunResult> RunCoreAsync(
        string scriptFile, string logName, IProgress<string>? output, TimeSpan? timeout)
    {
        string scriptPath = ResolvePath(scriptFile);
        if (!File.Exists(scriptPath))
        {
            return ScriptRunResult.NotStarted(scriptFile, $"Script not found: {scriptPath}");
        }

        DateTime startedAt = DateTime.Now;
        var stopwatch = Stopwatch.StartNew();
        using var log = ScriptLog.Open(LogsDirectory, $"{startedAt:yyyyMMdd-HHmmss}_{logName}.log");
        log.WriteHeader(scriptPath, startedAt);

        var psi = new ProcessStartInfo
        {
            FileName = Path.Combine(Environment.SystemDirectory, "cmd.exe"),
            // /d skips AutoRun commands. cmd strips the outer quotes and keeps the path quoted.
            Arguments = $"/d /c \"\"{scriptPath}\"\"",
            WorkingDirectory = ScriptsDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = ConsoleEncoding,
            StandardErrorEncoding = ConsoleEncoding,
        };

        using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
        var exited = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        process.Exited += (_, _) => exited.TrySetResult();
        process.OutputDataReceived += (_, e) => OnLine(e.Data);
        process.ErrorDataReceived += (_, e) => OnLine(e.Data);

        try
        {
            process.Start();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            log.Close($"NOT STARTED: {ex.Message}");
            return ScriptRunResult.NotStarted(scriptFile, $"Could not start cmd.exe: {ex.Message}");
        }

        _runningProcessId = process.Id;

        // Every script ends with PAUSE. Closing stdin gives it end-of-input, so it returns instead of waiting for a key.
        process.StandardInput.Close();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

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
        await DrainOutputAsync(process).ConfigureAwait(false);
        stopwatch.Stop();

        int? exitCode = timedOut ? null : process.ExitCode;
        log.Close(timedOut ? "TIMED OUT (process stopped)" : $"EXIT CODE {exitCode} · {stopwatch.Elapsed:hh\\:mm\\:ss}");

        return new ScriptRunResult
        {
            ScriptFile = scriptFile,
            StartedAt = startedAt,
            Duration = stopwatch.Elapsed,
            ExitCode = exitCode,
            TimedOut = timedOut,
            Output = log.Lines,
            LogFilePath = log.FilePath,
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

            if (log.Append(line))
            {
                output?.Report(line);
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

    /// <summary>Collects output lines and mirrors them to a log file. Safe to call from the output reader threads.</summary>
    private sealed class ScriptLog : IDisposable
    {
        private readonly object _sync = new();
        private readonly List<string> _lines = [];
        private StreamWriter? _writer;
        private bool _closed;

        private ScriptLog(string? filePath, StreamWriter? writer)
        {
            FilePath = filePath;
            _writer = writer;
        }

        public string? FilePath { get; }

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_sync)
                {
                    return _lines.ToArray();
                }
            }
        }

        public static ScriptLog Open(string directory, string fileName)
        {
            try
            {
                Directory.CreateDirectory(directory);
                string path = Path.Combine(directory, fileName);
                return new ScriptLog(path, new StreamWriter(path, append: false, new UTF8Encoding(false)) { AutoFlush = true });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // The run still works without a log file; the output stays in memory.
                return new ScriptLog(null, null);
            }
        }

        public void WriteHeader(string scriptPath, DateTime startedAt)
        {
            WriteRaw($"# Script  : {scriptPath}");
            WriteRaw($"# Started : {startedAt:yyyy-MM-dd HH:mm:ss}");
            WriteRaw($"# Elevated: {(Elevation.IsElevated ? "yes" : "no")}");
            WriteRaw(string.Empty);
        }

        /// <summary>Returns false once the log is closed (late output after the script finished).</summary>
        public bool Append(string line)
        {
            lock (_sync)
            {
                if (_closed)
                {
                    return false;
                }

                _lines.Add(line);
                TryWrite(line);
                return true;
            }
        }

        public void Close(string footer)
        {
            lock (_sync)
            {
                if (_closed)
                {
                    return;
                }

                TryWrite(string.Empty);
                TryWrite($"# {footer}");
                _closed = true;
                _writer?.Dispose();
                _writer = null;
            }
        }

        public void Dispose() => Close("CLOSED");

        private void WriteRaw(string line)
        {
            lock (_sync)
            {
                TryWrite(line);
            }
        }

        private void TryWrite(string line)
        {
            try
            {
                _writer?.WriteLine(line);
            }
            catch (IOException)
            {
                _writer = null;
            }
        }
    }
}
