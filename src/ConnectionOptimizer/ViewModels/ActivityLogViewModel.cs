using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Threading;

namespace ConnectionOptimizer.ViewModels;

public enum ActivityLevel
{
    Info,
    Success,
    Warning,
    Error,
}

public sealed record ActivityEntry(DateTime Timestamp, ActivityLevel Level, string Message)
{
    public string Time => Format.Clock(Timestamp);

    public string Glyph => Level switch
    {
        ActivityLevel.Success => "✓",
        ActivityLevel.Warning => "!",
        ActivityLevel.Error => "✕",
        _ => "›",
    };
}

/// <summary>What the app did in this session, shown in the ACTIVITY panel and mirrored to a daily file.</summary>
public sealed class ActivityLogViewModel
{
    private const int MaxEntries = 500;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly string? _filePath;

    public ActivityLogViewModel(string? logsDirectory)
    {
        if (logsDirectory is not null)
        {
            _filePath = Path.Combine(logsDirectory, $"activity-{DateTime.Now:yyyyMMdd}.log");
        }
    }

    public ObservableCollection<ActivityEntry> Entries { get; } = [];

    public void Info(string message) => Add(ActivityLevel.Info, message);

    public void Success(string message) => Add(ActivityLevel.Success, message);

    public void Warning(string message) => Add(ActivityLevel.Warning, message);

    public void Error(string message) => Add(ActivityLevel.Error, message);

    private void Add(ActivityLevel level, string message)
    {
        if (!_dispatcher.CheckAccess())
        {
            _dispatcher.BeginInvoke(() => Add(level, message));
            return;
        }

        var entry = new ActivityEntry(DateTime.Now, level, message);
        Entries.Add(entry);
        if (Entries.Count > MaxEntries)
        {
            Entries.RemoveAt(0);
        }

        AppendToFile(entry);
    }

    private void AppendToFile(ActivityEntry entry)
    {
        if (_filePath is null)
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.AppendAllText(_filePath, $"{entry.Timestamp:yyyy-MM-dd HH:mm:ss}  {entry.Level,-7}  {entry.Message}{Environment.NewLine}");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The on-screen log still works.
        }
    }
}
