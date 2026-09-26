namespace ConnectionOptimizer.ViewModels;

/// <summary>Every state the UI can show. Each one has its own glyph and word, not only a color.</summary>
public enum StatusKind
{
    Ready,
    Running,
    Optimizing,
    Checking,
    Applied,
    Completed,
    Active,
    Partial,
    Inactive,
    Error,
}
