namespace ConnectionOptimizer.ViewModels;

public enum StepState
{
    Pending,
    Running,
    Done,
    Failed,
    NotRun,
}

/// <summary>One step of the ACTIVATE ALL sequence.</summary>
public sealed class BatchStepViewModel(string number, string name) : ObservableObject
{
    private StepState _state = StepState.Pending;

    public string Number { get; } = number;
    public string Name { get; } = name;

    public StepState State
    {
        get => _state;
        set
        {
            if (SetProperty(ref _state, value))
            {
                OnPropertyChanged(nameof(StateText));
            }
        }
    }

    public string StateText => State switch
    {
        StepState.Running => "RUNNING…",
        StepState.Done => "✓ EXIT 0",
        StepState.Failed => "✕ FAILED",
        StepState.NotRun => "NOT RUN",
        _ => "PENDING",
    };
}
