using System.Collections.ObjectModel;
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

/// <summary>What the app did in this session, shown in the ACTIVITY panel. Kept in memory only, never saved.</summary>
public sealed class ActivityLogViewModel
{
    private const int MaxEntries = 500;

    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;

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
    }
}
