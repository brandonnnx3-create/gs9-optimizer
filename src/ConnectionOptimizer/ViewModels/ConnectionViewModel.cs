using ConnectionOptimizer.Services;

namespace ConnectionOptimizer.ViewModels;

public sealed class ConnectionViewModel : ObservableObject
{
    private const string Unknown = "—";

    private ConnectionInfo? _info;
    private bool _isLoading;
    private DateTime? _updatedAt;

    public bool IsLoading
    {
        get => _isLoading;
        set
        {
            if (SetProperty(ref _isLoading, value))
            {
                OnPropertyChanged(nameof(UpdatedText));
            }
        }
    }

    public bool HasConnection => _info?.Found == true;

    public string Medium => _info switch
    {
        null => "DETECTING",
        { Found: false } => "NONE",
        _ => _info.Medium,
    };

    public string AdapterName => HasConnection ? _info!.Name : Unknown;
    public string AdapterDescription => HasConnection ? _info!.Description : "No active network adapter found";
    public string SpeedValue => HasConnection ? Format.Speed(_info!.SpeedBitsPerSecond).Value : Unknown;
    public string SpeedUnit => HasConnection ? Format.Speed(_info!.SpeedBitsPerSecond).Unit : string.Empty;
    public StatusKind StatusKind => HasConnection && _info!.IsUp ? StatusKind.Active : StatusKind.Inactive;
    public string StatusText => HasConnection ? _info!.Status : _info is null ? "DETECTING…" : "NO CONNECTION";
    public string IPv4 => HasConnection ? _info!.IPv4 ?? Unknown : Unknown;
    public string Gateway => HasConnection ? _info!.Gateway ?? Unknown : Unknown;
    public string Dns => HasConnection && _info!.Dns.Count > 0 ? string.Join("  ·  ", _info.Dns.Take(3)) : Unknown;
    public string InterfaceGuid => HasConnection ? _info!.InterfaceGuid : Unknown;

    public string UpdatedText =>
        IsLoading ? "READING…"
        : _updatedAt is { } at ? $"UPDATED {Format.Clock(at)}"
        : string.Empty;

    public void Update(ConnectionInfo info)
    {
        _info = info;
        _updatedAt = DateTime.Now;
        OnPropertyChanged(string.Empty); // Every derived property changed.
    }
}
